using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameSystem;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States
{
    /// <summary>
    /// 舌卷回吞投技：抓取舌沿锁定线暴射，缠住玩家高速回卷，
    /// 饥饿者沿途撕咬，入口咀嚼三拍后向前吐出。
    /// 双触发：洗牌袋主动抓取 / 绕后被原版舌头拖到嘴边时升级。
    /// 网络形状：ai[3]=受害者whoAmI+1、ai[0]=演出时钟(服务端权威)；
    /// 位移与伤害全在受害者自己的客户端结算(镜像原版TheTongue)
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)WofStateIndex.TongueGrab, typeof(WofStateContext))]
    internal class WofTongueGrabState : WofStateBase
    {
        public override string StateName => "TongueGrab";
        public override WofStateIndex StateIndex => WofStateIndex.TongueGrab;

        #region 时间轴常量(绝对Timer刻)
        /// <summary>预告结束/甩舌开始</summary>
        internal const int LashStartTick = WofDirector.GrabTelegraph;
        /// <summary>回卷开始(甩舌窗口结束)</summary>
        internal const int ReelStartTick = LashStartTick + WofDirector.GrabLashFrames;
        /// <summary>咀嚼开始</summary>
        internal const int ChewStartTick = ReelStartTick + WofDirector.GrabReelFrames;
        /// <summary>吐出帧</summary>
        internal const int SpitTick = ChewStartTick + WofDirector.GrabChewFrames;
        /// <summary>恢复开始(舌头已回吞)</summary>
        internal const int RecoverStartTick = SpitTick + WofDirector.GrabSpitTail;
        /// <summary>正常总时长</summary>
        internal const int TotalTick = RecoverStartTick + WofDirector.GrabRecoverFrames;
        /// <summary>保底超时</summary>
        internal const int HardTimeoutTick = TotalTick + 44;

        /// <summary>三口咀嚼节拍(相对ChewStartTick)</summary>
        private static readonly int[] ChewBeats = [14, 42, 70];
        /// <summary>沿途撕咬节拍(相对ReelStartTick)</summary>
        private static readonly int[] BiteBeats = [16, 38];
        /// <summary>撕咬判定半径(受害者周围有活饥饿者才咬)</summary>
        private const float BiteRange = 520f;
        #endregion

        /// <summary>上一帧是否已进入回卷(各端本地，抓取确认一次性演出用)</summary>
        private bool snagPlayed;

        #region 对外契约(弹幕/演出玩家/运镜读取)
        /// <summary>咀嚼保持点：口器中心沿推进方向前伸</summary>
        internal static Vector2 MouthHold(NPC wall) {
            return wall.Center + new Vector2(wall.direction * WofDirector.GrabMouthInset, 0f);
        }

        /// <summary>读取受害者whoAmI，-1无</summary>
        internal static int VictimIndex(NPC wall) => (int)wall.ai[3] - 1;

        /// <summary>当前演出时钟(本地平滑推进+服务端纠偏)</summary>
        internal int GrabTimer => Timer;

        /// <summary>
        /// 获取正在进行的舌卷回吞：墙有效+状态索引匹配+接管在场，
        /// 返回本端状态机里的状态实例(其Timer即本端演出时钟)
        /// </summary>
        internal static bool TryGetActiveGrab(out NPC wall, out WofTongueGrabState state) {
            state = null;
            if (!WallOfFleshAI.TryGetWall(out wall)) {
                return false;
            }
            if (WallOfFleshAI.GetStateIndex(wall) != WofStateIndex.TongueGrab) {
                return false;
            }
            if (!wall.TryGetOverride(out Dictionary<Type, NPCOverride> overrides)
                || !overrides.TryGetValue(typeof(WallOfFleshAI), out NPCOverride ov)
                || ov is not WallOfFleshAI wofAI) {
                return false;
            }
            state = wofAI.CurrentMachineState as WofTongueGrabState;
            return state != null;
        }
        #endregion

        public override void OnEnter(WofStateContext context) {
            base.OnEnter(context);
            snagPlayed = false;
            NPC npc = context.Npc;

            if (!VaultUtils.isClient) {
                //绕后惩罚路径：受害者已被原版舌头拖到嘴边，跳过预告与甩舌直接开卷
                if (context.PendingGrabVictim >= 0 && context.PendingGrabVictim < Main.maxPlayers
                    && Main.player[context.PendingGrabVictim].Alives()) {
                    npc.ai[3] = context.PendingGrabVictim + 1;
                    Timer = ReelStartTick;
                    SpawnTongue(context, attached: true);
                }
                else {
                    npc.ai[3] = 0f;
                }
                context.PendingGrabVictim = -1;
                npc.ai[0] = Timer;
                npc.netUpdate = true;
            }

            if (!VaultUtils.isServer) {
                //区别于普通舌鞭的低沉双音开场
                SoundEngine.PlaySound(SoundID.Zombie10 with { Pitch = -0.55f, Volume = 1.1f }, npc.Center);
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.8f, Volume = 0.9f }, npc.Center);
            }
        }

        public override IWofState OnUpdate(WofStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            //客户端向服务端时钟纠偏(只快进不回拨，容忍15刻传输差)
            if (VaultUtils.isClient && npc.ai[0] > Timer + 15) {
                Timer = (int)npc.ai[0];
            }

            //服务端：受害者异常出口(死亡/掉线/传送离场) → 断投收舌
            if (!VaultUtils.isClient && Timer >= ReelStartTick && Timer < SpitTick) {
                int v = VictimIndex(npc);
                if (v < 0 || v >= Main.maxPlayers || !Main.player[v].active
                    || Main.player[v].dead || Main.player[v].ghost
                    || Main.player[v].Distance(MouthHold(npc)) > 1600f) {
                    npc.ai[3] = 0f;
                    Timer = RecoverStartTick;
                }
            }

            //服务端写时钟(随每10帧的周期netUpdate下发)
            if (!VaultUtils.isClient) {
                npc.ai[0] = Timer;
            }

            //保底超时
            if (Timer > HardTimeoutTick) {
                return new WofAdvanceState();
            }

            if (Timer < LashStartTick) {
                UpdateTelegraph(context);
            }
            else if (Timer < ReelStartTick) {
                UpdateLash(context);
            }
            else if (Timer < ChewStartTick) {
                UpdateReel(context);
            }
            else if (Timer < SpitTick) {
                UpdateChew(context);
            }
            else if (Timer < RecoverStartTick) {
                UpdateSpitTail(context);
            }
            else {
                context.AdvanceFactor = 0.6f;
                context.MouthCommand = 2;
                context.WallFlush = MathHelper.Lerp(0.6f, 0.3f,
                    (Timer - RecoverStartTick) / (float)WofDirector.GrabRecoverFrames);
                //恢复拍：口涎滴落
                if (!VaultUtils.isServer && Timer % 5 == 0) {
                    WofMotionFX.SpawnWallSeep(npc, 1.8f);
                }
                if (Timer >= TotalTick) {
                    return new WofAdvanceState();
                }
            }
            return null;
        }

        public override void OnExit(WofStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            npc.ai[3] = 0f;
            npc.ai[0] = 0f;
            if (!VaultUtils.isClient) {
                context.GrabCooldown = WofDirector.GrabCooldownFrames;
                context.PendingGrabVictim = -1;
                npc.netUpdate = true;
            }
        }

        #region 阶段更新
        /// <summary>预告：巨口洞开、血珠向喉内倒卷、双声递进警报，比普通舌鞭更长更重</summary>
        private void UpdateTelegraph(WofStateContext context) {
            NPC npc = context.Npc;
            float p = MathHelper.Clamp(Timer / (float)WofDirector.GrabTelegraph, 0f, 1f);
            context.AdvanceFactor = MathHelper.Lerp(0.55f, 0.2f, p);
            context.MouthCommand = 1;
            context.SetChargeState(5, p);
            context.WallFlush = 0.45f + 0.4f * p;

            if (VaultUtils.isServer) {
                return;
            }
            //血珠被倒吸进喉(蓄力语法：向心汇聚)
            if (Timer % 2 == 0 && p < 0.8f) {
                Vector2 from = npc.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(150f, 420f);
                if (WofMotionFX.OnScreen(from)) {
                    PRTLoader.NewParticle<PRT_Spark>(from, (npc.Center - from) * 0.06f,
                        WofMotionFX.BloodHot, Main.rand.NextFloat(0.8f, 1.4f))?.Configure(false, 15);
                }
            }
            //环波蓄势
            if (Timer == 26) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(npc.Center, Vector2.Zero,
                    WofMotionFX.BloodHot, 0.14f)?.Configure(0.14f, 1.2f, 22);
            }
            //双声递进警报(可读性阀门)
            if (Timer == 36) {
                SoundEngine.PlaySound(SoundID.Item171 with { Pitch = -0.3f, Volume = 0.95f }, npc.Center);
            }
            if (Timer == 46) {
                SoundEngine.PlaySound(SoundID.Item171 with { Pitch = 0.1f, Volume = 1.1f }, npc.Center);
            }
            if (Timer % 8 == 0) {
                WofMotionFX.CameraPunch(npc.Center, 0.9f + 2.6f * p * p, 9, "WofGrabCharge");
            }
        }

        /// <summary>甩舌窗口：舌体暴射(锁线不追踪)，服务端沿线扫描抓取；落空则跳恢复</summary>
        private void UpdateLash(WofStateContext context) {
            NPC npc = context.Npc;
            context.AdvanceFactor = 0.3f;
            context.MouthCommand = 1;
            context.WallFlush = 0.7f;

            //出舌帧
            if (Timer == LashStartTick) {
                if (!VaultUtils.isClient && VictimIndex(npc) < 0) {
                    SpawnTongue(context, attached: false);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Pitch = -0.75f, Volume = 1.2f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = 0.1f, Volume = 1f }, npc.Center);
                    WofMotionFX.SpawnBloodBurst(npc.Center + new Vector2(npc.direction * 46f, 0f), 1.1f,
                        new Vector2(npc.direction, 0f));
                    WofMotionFX.CameraPunch(npc.Center, 4.5f, 12, "WofGrabLash", new Vector2(npc.direction, 0f));
                }
            }

            //服务端沿线抓取扫描
            if (!VaultUtils.isClient && VictimIndex(npc) < 0) {
                TryServerGrabScan(context);
                //窗口末仍未命中 → 跳到恢复拍(落空收舌由弹幕自理)
                if (Timer == ReelStartTick - 1) {
                    Timer = RecoverStartTick;
                    npc.ai[0] = Timer;
                    npc.netUpdate = true;
                }
            }
        }

        /// <summary>回卷：绷紧顿帧后高速拽回，饥饿者沿途扑咬；位移由受害者客户端执行</summary>
        private void UpdateReel(WofStateContext context) {
            NPC npc = context.Npc;
            context.AdvanceFactor = 0.45f;
            context.MouthCommand = 1;
            context.WallFlush = 0.75f;

            int victim = VictimIndex(npc);
            if (victim < 0 || victim >= Main.maxPlayers) {
                return;
            }
            Player player = Main.player[victim];

            //抓取确认一次性演出(各端在跨入回卷时触发)
            if (!snagPlayed) {
                snagPlayed = true;
                if (!VaultUtils.isServer && player.Alives()) {
                    SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.45f, Volume = 1.2f }, player.Center);
                    SoundEngine.PlaySound(SoundID.Zombie10 with { Pitch = 0.2f, Volume = 1f }, npc.Center);
                    WofMotionFX.SpawnBloodBurst(player.Center, 0.8f);
                    if (Main.myPlayer == victim) {
                        WofGrabPerformancePlayer.RequestShake(6f, 16);
                    }
                    else {
                        WofMotionFX.CameraPunch(player.Center, 4f, 12, "WofGrabSnag");
                    }
                }
            }

            //沿途撕咬节拍
            int local = Timer - ReelStartTick;
            for (int i = 0; i < BiteBeats.Length; i++) {
                if (local == BiteBeats[i]) {
                    DoBiteBeat(context, player);
                }
            }

            //舌根拖拽血沫
            if (!VaultUtils.isServer && player.Alives() && Timer % 3 == 0) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(player.Center + Main.rand.NextVector2Circular(18f, 18f),
                    (npc.Center - player.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3f),
                    WofMotionFX.BloodMid, Main.rand.NextFloat(0.7f, 1.1f))?.Configure(Main.rand.Next(16, 28), 0.3f);
            }
        }

        /// <summary>咀嚼：口内三拍研磨，最后一口最重；伤害只在受害者客户端结算且保底不致死</summary>
        private void UpdateChew(WofStateContext context) {
            NPC npc = context.Npc;
            context.AdvanceFactor = 0.35f;
            context.WallFlush = 0.85f;

            int victim = VictimIndex(npc);
            int local = Timer - ChewStartTick;

            //咬合节拍附近紧咬定格，其余狂乱磨牙
            bool nearBeat = false;
            for (int i = 0; i < ChewBeats.Length; i++) {
                if (Math.Abs(local - ChewBeats[i]) <= 5) {
                    nearBeat = true;
                    break;
                }
            }
            context.MouthCommand = nearBeat ? 2 : 1;

            if (victim < 0 || victim >= Main.maxPlayers) {
                return;
            }
            Player player = Main.player[victim];

            for (int i = 0; i < ChewBeats.Length; i++) {
                if (local != ChewBeats[i]) {
                    continue;
                }
                bool finalBite = i == ChewBeats.Length - 1;
                int baseDamage = finalBite ? WofDirector.GrabChewFinalDamage : WofDirector.GrabChewDamage;

                //受害者本地结算(保底不处死)
                if (Main.myPlayer == victim) {
                    HurtVictimBeat(npc, player, baseDamage);
                    WofGrabPerformancePlayer.RequestShake(finalBite ? 8f : 5f, finalBite ? 18 : 12);
                }
                //全端演出
                if (!VaultUtils.isServer) {
                    Vector2 mouth = MouthHold(npc);
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.5f + i * 0.12f, Volume = 1.1f }, mouth);
                    SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.2f, Volume = 0.9f }, mouth);
                    WofMotionFX.SpawnBloodBurst(mouth, finalBite ? 1.3f : 0.8f, new Vector2(npc.direction, -0.3f));
                    if (finalBite) {
                        SoundEngine.PlaySound(SoundID.NPCDeath12 with { Pitch = -0.4f, Volume = 1f }, mouth);
                    }
                    if (Main.myPlayer != victim) {
                        WofMotionFX.CameraPunch(mouth, finalBite ? 5.5f : 3.5f, finalBite ? 16 : 10, "WofGrabChew");
                    }
                }
            }
        }

        /// <summary>吐出与回吞：终结帧把玩家向前掷出(速度由受害者客户端施加)，舌头随即回吞</summary>
        private void UpdateSpitTail(WofStateContext context) {
            NPC npc = context.Npc;
            context.AdvanceFactor = 0.5f;
            context.MouthCommand = 1;
            context.WallFlush = 0.7f;

            if (Timer != SpitTick) {
                return;
            }
            //吐出帧：全端最重一拍
            if (!VaultUtils.isServer) {
                Vector2 mouth = MouthHold(npc);
                WofMotionFX.MouthRoar(npc, 1.4f, playSound: false);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 1.1f }, npc.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath13 with { Pitch = -0.3f, Volume = 1.1f }, mouth);
                //向前的喷吐血锥
                for (int i = 0; i < 14; i++) {
                    Vector2 vel = new Vector2(npc.direction, 0f).RotatedBy(Main.rand.NextFloat(-0.55f, 0.55f))
                        * Main.rand.NextFloat(5f, 13f);
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(mouth, vel,
                        Color.Lerp(WofMotionFX.BloodMid, WofMotionFX.BloodHot, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.9f, 1.5f))?.Configure(Main.rand.Next(24, 40), 0.34f);
                }
                WofMotionFX.SpawnBloodBurst(mouth, 1.2f, new Vector2(npc.direction, -0.2f));
                int victim = VictimIndex(npc);
                if (Main.myPlayer == victim) {
                    WofGrabPerformancePlayer.RequestShake(9f, 20);
                }
                else {
                    WofMotionFX.CameraPunch(mouth, 6.5f, 18, "WofGrabSpit", new Vector2(npc.direction, 0f));
                }
            }
            //受害者标记保留到OnExit统一清除：受害者客户端时钟可能落后数刻，
            //在此清除会让击飞窗口读不到"轮到我"，退化成原地掉落
        }
        #endregion

        #region 工具
        /// <summary>服务端生成抓取舌；attached=绕后路径已缠住</summary>
        private void SpawnTongue(WofStateContext context, bool attached) {
            NPC npc = context.Npc;
            Vector2 dir;
            int victim = VictimIndex(npc);
            if (attached && victim >= 0 && Main.player[victim].Alives()) {
                dir = (Main.player[victim].Center - npc.Center).SafeNormalize(Vector2.UnitX * npc.direction);
            }
            else if (context.Target.Alives()) {
                //提前量锁线：出舌瞬间定格，此后不追踪(可躲避阀门)
                Vector2 predicted = context.Target.Center + context.Target.velocity * 14f;
                dir = (predicted - npc.Center).SafeNormalize(Vector2.UnitX * npc.direction);
            }
            else {
                dir = Vector2.UnitX * npc.direction;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir,
                ModContent.ProjectileType<WofGrabTongueProj>(),
                WallOfFleshAI.ScaleDamage(npc, WofDirector.GrabSnagDamage), 0f, Main.myPlayer,
                npc.whoAmI, attached ? 1f : 0f);
            npc.netUpdate = true;
        }

        /// <summary>服务端沿舌线扫描抓取候选，命中即锁定并跳到回卷</summary>
        private void TryServerGrabScan(WofStateContext context) {
            NPC npc = context.Npc;
            Projectile tongue = WofGrabTongueProj.FindForWall(npc.whoAmI);
            if (tongue == null) {
                return;
            }
            Vector2 dir = tongue.velocity.SafeNormalize(Vector2.UnitX * npc.direction);
            float reach = Math.Min((Timer - LashStartTick) * WofDirector.GrabExtendSpeed, WofDirector.GrabMaxReach);
            if (reach <= 0f) {
                return;
            }
            float point = 0f;
            foreach (Player p in Main.ActivePlayers) {
                if (!p.Alives() || p.ghost) {
                    continue;
                }
                if (!Collision.CheckAABBvLineCollision(p.position, p.Size,
                    npc.Center, npc.Center + dir * reach, 34f, ref point)) {
                    continue;
                }
                //抓取确认：写受害者+跳时钟，随NPC同步原子下发
                npc.ai[3] = p.whoAmI + 1;
                Timer = ReelStartTick;
                npc.ai[0] = Timer;
                npc.netUpdate = true;
                break;
            }
        }

        /// <summary>沿途撕咬：服务端令最近饥饿者扑咬，受害者本地小口结算，全端血沫</summary>
        private void DoBiteBeat(WofStateContext context, Player player) {
            NPC npc = context.Npc;
            if (!player.Alives()) {
                return;
            }
            //周围无活饥饿者则跳过(清了小怪的奖励)
            NPC nearest = null;
            float bestDist = BiteRange;
            foreach (var n in Main.ActiveNPCs) {
                if (n.type != NPCID.TheHungry) {
                    continue;
                }
                float d = n.Distance(player.Center);
                if (d < bestDist) {
                    bestDist = d;
                    nearest = n;
                }
            }
            if (nearest == null) {
                return;
            }

            //服务端：扑咬冲量(饥饿者AI是增量加速，冲量自然衰减)
            if (!VaultUtils.isClient) {
                nearest.velocity = (player.Center - nearest.Center).SafeNormalize(Vector2.Zero) * 11f;
                nearest.netUpdate = true;
            }
            //受害者本地：小口撕咬
            if (Main.myPlayer == player.whoAmI) {
                HurtVictimBeat(npc, player, WofDirector.GrabBiteDamage);
                WofGrabPerformancePlayer.RequestShake(3.5f, 10);
            }
            //全端血沫与咬声
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = 0.15f, Volume = 0.9f }, player.Center);
                WofMotionFX.SpawnBloodBurst(player.Center, 0.5f);
            }
        }

        /// <summary>
        /// 投技节拍伤害：穿透无敌帧(DD2OgreKnockback槽)但请求值封顶在当前生命-1，
        /// 残血只演不杀，满血玩家绝不会被一套投技处死
        /// </summary>
        private static void HurtVictimBeat(NPC wall, Player player, int baseDamage) {
            if (!player.Alives()) {
                return;
            }
            int scaled = WallOfFleshAI.ScaleDamage(wall, baseDamage);
            int capped = Math.Min(scaled, Math.Max(0, player.statLife - 1));
            if (capped < 1) {
                return;
            }
            player.Hurt(PlayerDeathReason.ByNPC(wall.whoAmI), capped, wall.direction,
                cooldownCounter: ImmunityCooldownID.DD2OgreKnockback, knockback: 0f);
        }
        #endregion
    }
}
