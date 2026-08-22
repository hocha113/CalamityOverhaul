using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States
{
    /// <summary>
    /// 投技公共契约：override ai 槽位（NetSend 掩码搭车 SendExtraAI，
    /// 与状态槽 npc.ai[2] 同包原子到达）与舞台几何
    /// </summary>
    internal static class FishronGrabFacts
    {
        /// <summary>受害者 whoAmI+1，0=未抓取</summary>
        internal const int SlotVictim = 0;
        /// <summary>漩涡锚点 X（水面/地表）</summary>
        internal const int SlotAnchorX = 1;
        /// <summary>漩涡锚点 Y</summary>
        internal const int SlotAnchorY = 2;
        /// <summary>连段方向种子 0~15（服务端掷骰，各端同帧同舞步）</summary>
        internal const int SlotSeed = 3;

        /// <summary>涡心抬升：钉人点在锚点上方，永不入砖</summary>
        internal const float HeartRise = 90f;
        /// <summary>抓取判定核心半径</summary>
        internal const float CoreRadius = 120f;
        /// <summary>抽吸力场半径</summary>
        internal const float SuctionRadius = 640f;

        internal static Vector2 ReadAnchor(DukeFishronAI ov)
            => new(ov.ai[SlotAnchorX], ov.ai[SlotAnchorY]);

        internal static Vector2 Heart(Vector2 anchor) => anchor - new Vector2(0f, HeartRise);

        internal static int ReadVictim(DukeFishronAI ov) => (int)ov.ai[SlotVictim] - 1;

        /// <summary>服务端解除抓取：清受害者槽并即时广播</summary>
        internal static void ReleaseServer(DukeFishronAI ov) {
            ov.ai[SlotVictim] = 0f;
            ov.npc.netUpdate = true;
        }
    }

    /// <summary>
    /// 投技·蓄涡抽吸：公爵在目标脚下唤起大漩涡，抽吸把人往涡心拖
    /// 逆流游出力场即可脱身；到点仍滞留涡心者被卷入涡底（转入连段状态）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.VortexSnare, typeof(FishronStateContext))]
    internal class FishronVortexSnareState : FishronStateBase
    {
        public override string StateName => "VortexSnare";
        public override FishronStateIndex StateIndex => FishronStateIndex.VortexSnare;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        /// <summary>蓄涡前摇（专属视觉+咆哮，无任何判定）</summary>
        internal const int SuctionStart = 44;
        /// <summary>抓取裁决帧</summary>
        internal const int CommitTick = 116;
        private const int CollapseEnd = 148;
        /// <summary>空振冷却（25s）；命中冷却在连段状态加码</summary>
        private const int WhiffCooldown = 1500;
        #endregion

        /// <summary>投技放行门：阶段、冷却、时停、演出互斥全在此</summary>
        internal static bool CanTrigger(FishronStateContext ctx) {
            if (!ctx.PhaseTwoStarted || ctx.GrabCooldown > 0) {
                return false;
            }
            if (!ctx.Target.Alives()) {
                return false;
            }
            //世界时停期间不出投技
            if (TimeFreezeSystem.IsAnyGlobalFreezeActive) {
                return false;
            }
            //演出期间不出投技（专用服务器上 IsPlaying 恒 false，单机真实生效）
            if (!VaultUtils.isServer && CutsceneDirector.IsPlaying) {
                return false;
            }
            return true;
        }

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            //先按空振装填冷却，命中后由连段状态覆写加码
            context.GrabCooldown = WhiffCooldown;

            NPC npc = context.Npc;
            if (!VaultUtils.isClient && npc.TryGetOverride(out DukeFishronAI ov)) {
                //锚点：目标脚下的水面/地表，悬空竞技场限深兜底
                Vector2 surf = FishronMotionFX.FindSurfaceBelow(
                    context.Target.Center - new Vector2(0f, 40f), out _);
                if (surf.Y - context.Target.Center.Y > 520f) {
                    surf.Y = context.Target.Center.Y + 520f;
                }
                ov.ai[FishronGrabFacts.SlotVictim] = 0f;
                ov.ai[FishronGrabFacts.SlotAnchorX] = surf.X;
                ov.ai[FishronGrabFacts.SlotAnchorY] = surf.Y;
                ov.ai[FishronGrabFacts.SlotSeed] = Main.rand.Next(16);
                npc.netUpdate = true;

                //漩涡舞台弹幕（纯演出，无接触伤害）
                Projectile.NewProjectile(npc.GetSource_FromAI(), surf, Vector2.Zero,
                    ModContent.ProjectileType<FishronVortexProj>(), 0, 0f, Main.myPlayer,
                    npc.whoAmI);
            }

            //涡吼起手：低啸+闷雷，宣告非常规招式
            SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 1.15f, Pitch = -0.55f }, npc.Center);
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            //漩涡才是威胁，本体不带接触伤害
            npc.damage = 0;

            if (!npc.TryGetOverride(out DukeFishronAI ov)) {
                //覆写异常：直接撤退（服务端裁决）
                return VaultUtils.isClient ? null : new FishronHoverState();
            }
            Vector2 anchor = FishronGrabFacts.ReadAnchor(ov);
            Vector2 heart = FishronGrabFacts.Heart(anchor);
            int side = ((int)ov.ai[FishronGrabFacts.SlotSeed] & 1) == 0 ? 1 : -1;

            //目标死亡/离场：撤涡回悬停
            if (!player.Alives() && !VaultUtils.isClient) {
                FishronGrabFacts.ReleaseServer(ov);
                return new FishronHoverState();
            }

            //幕一：蓄涡前摇，奔赴漩涡侧翼，咆哮压场
            if (Timer <= SuctionStart) {
                float p = Timer / (float)SuctionStart;
                Vector2 goal = anchor + new Vector2(side * 430f, -190f);
                Vector2 desired = (goal - npc.Center).SafeNormalize(Vector2.Zero)
                    * MathHelper.Lerp(19f, 6f, p);
                npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.12f);
                FaceBody(npc, anchor, 0.12f);
                context.SetChargeState(2, p);
                context.FrameCommand = Timer < 22 ? 1 : 0;
                FishronStormSky.PushRainBoost(0.2f * p);

                //涡面初旋的水雾
                if (!VaultUtils.isServer && Timer % 4 == 0) {
                    FishronMotionFX.SpawnMist(anchor + Main.rand.NextVector2Circular(120f, 20f),
                        -Vector2.UnitY * 0.8f, 0.9f);
                }
                return null;
            }

            //幕二：抽吸窗口，吸力由 FishronGrabPlayer 各端本地施加，这里只做演出
            if (Timer < CommitTick) {
                float suction = (Timer - SuctionStart) / (float)(CommitTick - SuctionStart);

                //绕涡环游施压，半径缓收
                float angle = side * Timer * 0.02f + (side > 0 ? 0f : MathHelper.Pi);
                Vector2 orbit = anchor + angle.ToRotationVector2() * new Vector2(440f - suction * 60f, 240f)
                    - new Vector2(0f, 140f);
                npc.velocity = Vector2.Lerp(npc.velocity,
                    (orbit - npc.Center).SafeNormalize(Vector2.Zero) * 11f, 0.08f);
                FaceBody(npc, player.Center, 0.1f);

                context.SetChargeState(3, suction);
                context.FrameCommand = suction > 0.78f ? 1 : 0;
                context.StormBoost = 0.12f * suction;
                FishronStormSky.PushRainBoost(0.25f * suction);

                //周期涡吼：音高随抽吸攀升（定位声，旁观者距离衰减）
                if (Timer % 24 == 0) {
                    SoundEngine.PlaySound(SoundID.Item96 with {
                        Volume = 0.55f + suction * 0.3f,
                        Pitch = -0.6f + suction * 0.45f,
                        MaxInstances = 3
                    }, anchor);
                }
                //末 20 帧锁定警示：涡心白闪脉冲（与预告线 LockTime 同语法）
                if (Timer == CommitTick - 20) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.9f, Pitch = -0.35f }, anchor);
                    if (!VaultUtils.isServer) {
                        FishronMotionFX.SpawnSplashBurst(heart, 0.7f, playSound: false);
                    }
                }
                return null;
            }

            //裁决帧：核心半径内有人则卷入（服务端）
            if ((int)Timer == CommitTick && !VaultUtils.isClient) {
                int victim = -1;
                float best = float.MaxValue;
                foreach (Player p in Main.ActivePlayers) {
                    if (!p.Alives() || p.ghost) {
                        continue;
                    }
                    float dist = p.Distance(heart);
                    if (dist <= FishronGrabFacts.CoreRadius && dist < best) {
                        best = dist;
                        victim = p.whoAmI;
                    }
                }
                if (victim >= 0) {
                    ov.ai[FishronGrabFacts.SlotVictim] = victim + 1;
                    npc.netUpdate = true;
                    //公平阀：卷入即清场上气泡，钉住期不吃杂兵暗亏
                    DukeFishronAI.ClearMinions(alsoTornado: false);
                    return new FishronVortexGrabState();
                }
            }

            //幕三：空振坍缩，客户端留 8 帧宽限等抓取包，未到即本地播坍缩
            if ((int)Timer == CommitTick + 8) {
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.9f, Pitch = -0.4f, MaxInstances = 3 }, anchor);
                if (!VaultUtils.isServer) {
                    FishronMotionFX.SpawnSplashBurst(anchor, 1.4f, playSound: false);
                }
            }
            //悻悻拉开身位
            Vector2 away = player.Center + new Vector2(side * 380f, -220f);
            npc.velocity = Vector2.Lerp(npc.velocity,
                (away - npc.Center).SafeNormalize(Vector2.Zero) * 9f, 0.07f);
            FaceBody(npc, player.Center, 0.09f);

            if (Timer >= CollapseEnd && !VaultUtils.isClient) {
                return new FishronHoverState();
            }
            return null;
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.Npc.damage = context.Npc.defDamage;
        }
    }

    /// <summary>
    /// 投技·涡底猎杀：卷入涡心拖入水下（镜头入水+音效闷化），
    /// 猎鲨绕圈三轮鳍击掠过，深潜死寂一拍，自下而上破水把玩家顶飞上天收尾。
    /// 位移与锁控全在被抓者客户端（FishronGrabPlayer），本状态只管 boss 编舞与拍面结算
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.VortexGrab, typeof(FishronStateContext))]
    internal class FishronVortexGrabState : FishronStateBase
    {
        public override string StateName => "VortexGrab";
        public override FishronStateIndex StateIndex => FishronStateIndex.VortexGrab;
        public override bool AllowFarSnap => false;

        #region 节拍表（tick，自抓取帧起）
        /// <summary>卷入顿帧</summary>
        internal const int HitStopEnd = 8;
        /// <summary>拖入涡底就位</summary>
        internal const int DragEnd = 40;
        /// <summary>三轮鳍击起点</summary>
        internal const int PassStart = 68;
        /// <summary>单轮长度（46 帧 &gt; 受击无敌 40 帧，保证三拍都能结算）</summary>
        internal const int PassLength = 46;
        internal const int PassCount = 3;
        /// <summary>每轮前摇（掠角亮相+尖啸警示）</summary>
        internal const int PassAimTime = 18;
        /// <summary>命中拍在轮内的位置</summary>
        internal const int PassHurtOffset = 21;
        /// <summary>深潜蓄力（预静默拍）</summary>
        internal const int DiveStart = 206;
        /// <summary>破水顶飞，玩家控制在此帧交还</summary>
        internal const int LaunchTick = 232;
        /// <summary>冲天升势耗尽的空中刹停拍</summary>
        internal const int StallTick = 258;
        internal const int TotalTime = 300;
        private const int HardTimeout = 360;
        /// <summary>命中冷却（40s）</summary>
        private const int SuccessCooldown = 2400;
        #endregion

        /// <summary>已结算的连段拍序号，防重复扣血</summary>
        private int lastHurtPass = -1;
        private bool launched;

        public FishronVortexGrabState() {
        }

        /// <summary>单拍鳍击的原始伤害（走 Hurt 常规减伤）</summary>
        internal static int PassDamage(NPC npc) => Math.Max(20, (int)(npc.defDamage * 0.30f));

        /// <summary>第 k 轮的进场方位角：横掠→反侧下切→自下而上，铺垫破水收尾</summary>
        internal static float PassApproachAngle(int seed, int k) {
            int side = (seed & 1) == 0 ? 1 : -1;
            return k switch {
                0 => side > 0 ? 0f : MathHelper.Pi,
                1 => (side > 0 ? MathHelper.Pi : 0f) + side * 0.45f,
                _ => MathHelper.PiOver2 + side * 0.35f,
            };
        }

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            context.GrabCooldown = SuccessCooldown;
            lastHurtPass = -1;
            launched = false;
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Timer++;
            //连段全程零接触伤害：三拍都走剧本结算，预算可控
            npc.damage = 0;

            if (!npc.TryGetOverride(out DukeFishronAI ov)) {
                return VaultUtils.isClient ? null : new FishronHoverState();
            }
            Vector2 anchor = FishronGrabFacts.ReadAnchor(ov);
            Vector2 heart = FishronGrabFacts.Heart(anchor);
            int seed = (int)ov.ai[FishronGrabFacts.SlotSeed];
            int victimIdx = FishronGrabFacts.ReadVictim(ov);
            Player victim = victimIdx >= 0 && victimIdx < Main.maxPlayers ? Main.player[victimIdx] : null;

            //异常出口（服务端裁决）：受害者死亡/掉线/被传送远离、硬超时，破水后受害者本就自由
            if (!VaultUtils.isClient) {
                bool victimGone = victim == null || !victim.Alives()
                    || victim.Distance(heart) > 2200f;
                if ((victimGone && Timer < LaunchTick) || Timer >= HardTimeout) {
                    FishronGrabFacts.ReleaseServer(ov);
                    return new FishronHoverState();
                }
            }

            context.StormBoost = 0.15f;
            FishronStormSky.PushRainBoost(0.2f);

            //幕一：卷入顿帧，世界停一拍，只有水花在飞
            if (Timer <= HitStopEnd) {
                npc.velocity = Vector2.Zero;
                context.FrameCommand = 1;
                if ((int)Timer == 1) {
                    SoundEngine.PlaySound(SoundID.Splash with { Volume = 1.1f, Pitch = -0.5f }, heart);
                    SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 1.2f, Pitch = 0.1f }, heart);
                    FishronStormSky.PushFlash(0.5f, heart);
                    FishronMotionFX.CameraPunch(heart, 8f, 14, "FishronGrabYank");
                    FishronGrabPlayer.RequestShake(7f, 16);
                    if (!VaultUtils.isServer) {
                        FishronMotionFX.SpawnSplashBurst(heart, 1.7f, playSound: false);
                    }
                }
                return null;
            }

            //幕二：拖入涡底，绕涡贴面游走，宣示所有权
            if (Timer <= DragEnd) {
                float p = (Timer - HitStopEnd) / (float)(DragEnd - HitStopEnd);
                float angle = seed * 0.4f + p * MathHelper.TwoPi * 0.8f;
                Vector2 goal = heart + angle.ToRotationVector2() * MathHelper.Lerp(340f, 300f, p);
                npc.velocity = Vector2.Lerp(npc.velocity,
                    (goal - npc.Center).SafeNormalize(Vector2.Zero) * 22f, 0.16f);
                AimBodyAlongVelocity(npc);
                context.FrameCommand = 2;

                //受害者没顶的闷响拍（世界声压低，读作水面之下）
                if ((int)Timer == DragEnd - 6) {
                    SoundEngine.PlaySound(SoundID.Drown with { Volume = 0.85f, Pitch = -0.3f }, heart);
                }
                if (!VaultUtils.isServer && Timer % 3 == 0) {
                    FishronMotionFX.SpawnSprayCone(heart + Main.rand.NextVector2Circular(60f, 30f),
                        -Vector2.UnitY, 1, 2f, 6f, 0.9f, 0.8f);
                }
                return null;
            }

            //幕三：三轮鳍击，前摇亮相→直线掠过命中→弧线甩出
            if (Timer < DiveStart) {
                int t = (int)Timer - PassStart;
                if (t < 0) {
                    //呼吸拍：绕涡缓游，让受害者读清节奏
                    OrbitAround(npc, heart, seed, 300f);
                    return null;
                }
                int k = Math.Min(t / PassLength, PassCount - 1);
                int local = t - k * PassLength;
                float approach = PassApproachAngle(seed, k);
                Vector2 aimPos = heart + approach.ToRotationVector2() * 310f;

                if (local < PassAimTime) {
                    //前摇：吸附到掠角位，末段咬合定帧+尖啸
                    float p = local / (float)PassAimTime;
                    npc.velocity = Vector2.Lerp(npc.velocity,
                        (aimPos - npc.Center).SafeNormalize(Vector2.Zero)
                        * Math.Min((aimPos - npc.Center).Length() * 0.22f, 34f), 0.3f);
                    FaceBody(npc, heart, 0.3f);
                    context.SetChargeState(1, p);
                    context.DashDirection = (heart - aimPos).SafeNormalize(Vector2.UnitY);
                    if (local >= PassAimTime - 6) {
                        context.FrameCommand = 1;
                    }
                    if (local == PassAimTime - 6) {
                        SoundEngine.PlaySound(SoundID.NPCHit14 with { Volume = 0.8f, Pitch = 0.35f, MaxInstances = 3 }, npc.Center);
                    }
                    //收束水汽：蓄力语法
                    if (!VaultUtils.isServer && local % 2 == 0) {
                        FishronMotionFX.SpawnChargeGatherFX(npc.Center, p, 120f);
                    }
                }
                else if (local < PassAimTime + 8) {
                    //爆发：贯穿涡心的直线掠过
                    if (local == PassAimTime) {
                        Vector2 dir = (heart - npc.Center).SafeNormalize(Vector2.UnitY);
                        npc.velocity = dir * 52f;
                        FishronMotionFX.SpawnDashBurst(npc.Center, dir, 0.9f);
                    }
                    AimBodyAlongVelocity(npc);
                    context.FrameCommand = 2;

                    //命中拍：只在受害者自己的客户端结算（位置/生命皆客户端权威）
                    if (local == PassHurtOffset && lastHurtPass < k) {
                        lastHurtPass = k;
                        ResolvePassHit(npc, victim, victimIdx, heart, k);
                    }
                    if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                        FishronMotionFX.SpawnSprayCone(npc.Center,
                            -npc.velocity.SafeNormalize(Vector2.UnitY), 1, 3f, 8f, 0.5f, 0.85f);
                    }
                }
                else {
                    //余韵：弧线甩出+刹车泡沫
                    int side = (seed & 1) == 0 ? 1 : -1;
                    npc.velocity = npc.velocity.RotatedBy(side * 0.07f) * 0.93f;
                    AimBodyAlongVelocity(npc);
                    if (!VaultUtils.isServer && local % 3 == 0) {
                        FishronMotionFX.SpawnBrakeSpray(npc);
                    }
                }
                return null;
            }

            //幕四：深潜蓄力，沉入涡底不见，雨声骤停，预静默拍
            if (Timer < LaunchTick) {
                Vector2 deep = anchor + new Vector2((float)Math.Sin(Timer * 0.11f) * 40f, 420f);
                npc.velocity = Vector2.Lerp(npc.velocity,
                    (deep - npc.Center).SafeNormalize(Vector2.Zero) * 20f, 0.12f);
                AimBodyAlongVelocity(npc);
                //没入深水：现有剪影绘制承接
                npc.alpha = Math.Min(npc.alpha + 14, 200);
                context.FrameCommand = 2;
                if ((int)Timer == DiveStart + 16) {
                    FishronStormSky.PushRainCut(26);
                    SoundEngine.PlaySound(SoundID.Drown with { Volume = 0.6f, Pitch = -0.7f }, anchor);
                }
                return null;
            }

            //破水帧：自下而上把玩家顶飞上天
            if (!launched && Timer >= LaunchTick) {
                launched = true;
                npc.Center = anchor + new Vector2(0f, 340f);
                npc.velocity = new Vector2(0f, -44f);
                npc.alpha = 0;
                if (!VaultUtils.isClient) {
                    //受害者解除钉住；顶飞速度由受害者客户端按本地节拍自施
                    FishronGrabFacts.ReleaseServer(ov);
                }
                SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 1.3f, Pitch = 0.4f }, anchor);
                SoundEngine.PlaySound(SoundID.Item96 with { Volume = 1f, Pitch = -0.2f, MaxInstances = 3 }, anchor);
                FishronStormSky.PushFlash(0.9f, anchor);
                FishronMotionFX.CameraPunch(anchor, 14f, 22, "FishronGrabBreach", Vector2.UnitY);
                FishronGrabPlayer.RequestShake(11f, 20);
                if (!VaultUtils.isServer) {
                    FishronMotionFX.SpawnSplashBurst(anchor, 3.2f);
                    //喷泉水柱：破水的垂直能量
                    FishronMotionFX.SpawnSprayCone(anchor, -Vector2.UnitY, 26, 9f, 24f, 0.5f, 1.3f);
                }
            }

            //幕五：破水冲天，强拖拽泄力，升势在浪脊上耗尽（26 帧从 44 衰到 ~3）
            if (Timer < StallTick) {
                npc.velocity *= 0.90f;
                npc.velocity.Y += 0.12f;
                AimBodyAlongVelocity(npc);
                context.FrameCommand = 2;
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    FishronMotionFX.SpawnSprayCone(npc.Center,
                        -npc.velocity.SafeNormalize(Vector2.UnitY), 1, 2f, 7f, 0.5f, 0.8f);
                }
                return null;
            }
            if ((int)Timer == StallTick) {
                //空中刹停拍：甩尽一身水，悬在高点
                npc.velocity *= 0.3f;
                FishronMotionFX.CameraPunch(npc.Center, 4f, 10, "FishronGrabStall");
                if (!VaultUtils.isServer) {
                    FishronMotionFX.SpawnSprayCone(npc.Center, -Vector2.UnitY, 10, 3f, 10f, MathHelper.Pi, 1f);
                }
            }

            //幕六：力竭喘息，奖励输出窗口
            Player target = context.Target;
            npc.velocity *= 0.92f;
            if (target.Alives()) {
                FaceBody(npc, target.Center, 0.06f);
            }
            context.StormBoost = -0.1f;
            if ((int)Timer == StallTick + 12) {
                SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 0.7f, Pitch = -0.5f }, npc.Center);
            }

            if (Timer >= TotalTime && !VaultUtils.isClient) {
                return new FishronHoverState();
            }
            return null;
        }

        /// <summary>呼吸拍绕涡缓游</summary>
        private void OrbitAround(NPC npc, Vector2 heart, int seed, float radius) {
            int side = (seed & 1) == 0 ? 1 : -1;
            float angle = seed * 0.4f + Timer * 0.045f * side;
            Vector2 goal = heart + angle.ToRotationVector2() * radius;
            npc.velocity = Vector2.Lerp(npc.velocity,
                (goal - npc.Center).SafeNormalize(Vector2.Zero) * 14f, 0.1f);
            FaceBody(npc, heart, 0.12f);
        }

        /// <summary>
        /// 单拍命中结算：伤害只在受害者自己的客户端 Hurt（生命客户端权威，命中拍与本端画面严格同帧）；
        /// 视觉水花各端都放。剧本伤害尊重无敌帧（闪避饰品照常生效），残血跳拍，满血不可能被投技处死
        /// </summary>
        private static void ResolvePassHit(NPC npc, Player victim, int victimIdx, Vector2 heart, int passIndex) {
            //命中重音：所有端可见
            SoundEngine.PlaySound(SoundID.NPCHit14 with { Volume = 1f, Pitch = -0.2f, MaxInstances = 3 }, heart);
            FishronMotionFX.CameraPunch(heart, 5f, 10, "FishronGrabPass" + passIndex);
            FishronGrabPlayer.RequestShake(4.5f, 10);
            FishronStormSky.PushFlash(0.25f, heart);
            if (!VaultUtils.isServer) {
                FishronMotionFX.SpawnSplashBurst(heart, 1f, playSound: false);
            }

            //只在受害者本端结算生命
            if (Main.dedServ || victimIdx != Main.myPlayer || victim == null || !victim.Alives()) {
                return;
            }
            if (victim.immune) {
                return;
            }
            int raw = PassDamage(npc);
            //公平阀：预减伤前的原始值都打不死才出手，宁可空刀不追杀
            if (victim.statLife <= raw + 5) {
                return;
            }
            int hitDir = Math.Sign(npc.velocity.X);
            if (hitDir == 0) {
                hitDir = 1;
            }
            victim.Hurt(PlayerDeathReason.ByNPC(npc.whoAmI), raw, hitDir);
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;
            npc.alpha = 0;
            //兜底解除：任何路径离开本状态都不许把人钉在涡里
            if (!VaultUtils.isClient && npc.TryGetOverride(out DukeFishronAI ov)) {
                FishronGrabFacts.ReleaseServer(ov);
            }
        }
    }
}
