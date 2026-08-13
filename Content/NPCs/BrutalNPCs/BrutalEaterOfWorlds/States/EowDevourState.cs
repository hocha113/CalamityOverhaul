using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.States
{
    /// <summary>
    /// 投技·生吞入腹：入土→锁点长预警(巨口预兆)→垂直破土张口吞人→
    /// 咬合顿帧→携人钻回地底(受害者镜头入地转暗)→腹内挤压三拍→破土把人喷上天。<br/>
    /// 失手则拱弧摔回地面，留出长惩罚窗口。全程头部接触伤害为0，伤害只来自挤压拍(受害端结算)。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EowStateIndex.Devour, typeof(EowStateContext))]
    internal class EowDevourState : EowStateBase
    {
        public override string StateName => "Devour";
        public override EowStateIndex StateIndex => EowStateIndex.Devour;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int DiveMaxTime = 60;
        /// <summary>预警时长(≥40公平线)</summary>
        internal const int AmbushTime = 52;
        /// <summary>抓取窗口(破土起若干帧，与可见冲程对齐)</summary>
        private const int GrabWindow = 22;
        private const int BreachMaxTime = 26;
        internal const int HoldTime = 10;
        private const int GorgeMaxTime = 46;
        internal const int SqueezeTime = 84;
        /// <summary>挤压拍时刻表</summary>
        internal static readonly int[] BeatFrames = [16, 42, 68];
        private const int EjectMaxTime = 44;
        private const int WhiffTime = 66;
        private const int RecoverTime = 56;
        /// <summary>全状态保底超时</summary>
        private const int HardTimeout = 560;
        private const float BreachSpeed = 54f;
        private const float EjectSpeed = 56f;
        /// <summary>抓取判定半径(点到头部本帧扫掠线段)</summary>
        private const float GrabRadius = 110f;
        #endregion

        /// <summary>吞噬相位槽值：与 EowStateContext.GrabPhase 对齐</summary>
        internal enum GrabSlotPhase
        {
            None = 0,
            Hold = 1,
            Gorge = 2,
            Squeeze = 3,
            EjectCarry = 4,
            EjectLaunched = 5,
        }

        private enum Phase
        {
            DiveIn = 0,
            Ambush = 1,
            Breach = 2,
            GrabHold = 3,
            Gorge = 4,
            Squeeze = 5,
            Eject = 6,
            Whiff = 7,
            Recover = 8,
        }

        private Phase phase;
        private int totalTimer;
        private float groundY;
        private float lockX;
        /// <summary>喷出段各端本地计算的地表线</summary>
        private float ejectSurfaceY;
        private bool entryFired;
        private bool breachFired;
        private bool gorgeEntryFired;
        private bool ejectBreachFired;
        private bool whiffReentryFired;
        /// <summary>挤压拍表现观察值(-1=未初始化，防中途加入补爆)</summary>
        private int lastSeenBeat = -1;
        /// <summary>距上一次观察到挤压拍的本地帧数(驱动压缩包络)</summary>
        private int beatFxTimer = 999;

        public EowDevourState() {
        }

        #region 入场条件
        /// <summary>投技可否开场(权威端裁定)；不满足时选择器退回普通伏击</summary>
        internal static bool CanBegin(EowStateContext context) {
            if (context?.Npc == null || !context.Target.Alives() || context.Target.shimmering) {
                return false;
            }
            if (context.Npc.Distance(context.Target.Center) > 2200f) {
                return false;
            }
            //分裂未收拢不吞(体节布局口径混乱)
            if (context.SplitGroups > 1) {
                return false;
            }
            //时停期间不开场
            if (TimeFreezeSystem.IsAnyGlobalFreezeActive || TimeFreezeSystem.IsFrozen(context.Npc)) {
                return false;
            }
            //单机端权威即本地：运镜播放中不开场(服务端 CurrentClip 恒null，不受影响)
            if (!VaultUtils.isServer && CutsceneDirector.CurrentClip != null) {
                return false;
            }
            return true;
        }
        #endregion

        public override void OnEnter(EowStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = false;
            phase = Phase.DiveIn;
            totalTimer = 0;
            entryFired = false;
            breachFired = false;
            gorgeEntryFired = false;
            ejectBreachFired = false;
            whiffReentryFired = false;
            lastSeenBeat = -1;
            beatFxTimer = 999;

            //权威端清残余吞噬声明
            if (!VaultUtils.isClient) {
                context.GrabTargetWho = -1;
                context.GrabPhase = 0;
                context.GrabBeat = 0;
            }

            Vector2 anchor = context.Target.Alives() ? context.Target.Center : context.Npc.Center;
            groundY = EowMotionFX.FindGroundBelow(anchor).Y;
            lockX = anchor.X;
            EowMotionFX.PlayRoar(context.Npc.Center, -0.7f, 1.0f);
        }

        public override IEowState OnUpdate(EowStateContext context) {
            NPC npc = context.Npc;

            Tick();
            totalTimer++;

            //吞或不吞，头都不走接触伤害
            npc.damage = 0;

            //客户端相位仲裁：抓没抓住只听同步槽，本地绝不自行裁定
            FollowAuthoritySlots(context);

            //权威端：被抓者失效(死亡/掉线/被强制传送)立即断投
            if (!VaultUtils.isClient && IsGrabTrack(phase)) {
                Player victim = GetVictim(context);
                if (victim == null || !victim.Alives() || victim.Distance(npc.Center) > 1600f) {
                    AbortGrab(context);
                }
            }

            //观察挤压拍变化(各端本地表现锚定同步事件)
            WatchBeatChanges(context);

            switch (phase) {
                case Phase.DiveIn:
                    UpdateDiveIn(context);
                    break;
                case Phase.Ambush:
                    UpdateAmbush(context);
                    break;
                case Phase.Breach:
                    UpdateBreach(context);
                    break;
                case Phase.GrabHold:
                    UpdateGrabHold(context);
                    break;
                case Phase.Gorge:
                    UpdateGorge(context);
                    break;
                case Phase.Squeeze:
                    UpdateSqueeze(context);
                    break;
                case Phase.Eject:
                    UpdateEject(context);
                    break;
                case Phase.Whiff:
                    if (UpdateWhiff(context)) {
                        return new EowWeaveState();
                    }
                    break;
                case Phase.Recover:
                    if (UpdateRecover(context)) {
                        return new EowWeaveState();
                    }
                    break;
            }

            //保底超时：无论卡在哪一相位都强制收场
            if (totalTimer > HardTimeout && !VaultUtils.isClient) {
                AbortGrab(context);
                return new EowWeaveState();
            }

            return null;
        }

        #region 相位机构
        private static bool IsGrabTrack(Phase p) {
            return p is Phase.GrabHold or Phase.Gorge or Phase.Squeeze or Phase.Eject;
        }

        private Player GetVictim(EowStateContext context) {
            int who = context.GrabTargetWho;
            if (who < 0 || who >= Main.maxPlayers) {
                return null;
            }
            return Main.player[who];
        }

        private void SwitchPhase(EowStateContext context, Phase next) {
            phase = next;
            Timer = 0;
            EnterPhaseFX(context, next);
        }

        /// <summary>客户端跟随权威吞噬相位槽(抓取裁定/挤压/释放全由服务端事件驱动)</summary>
        private void FollowAuthoritySlots(EowStateContext context) {
            if (!VaultUtils.isClient) {
                return;
            }
            int slotPhase = context.Npc.TryGetOverride<EowHeadAI>(out var h)
                ? (int)h.ai[EowHeadAI.SlotGrabPhase] : context.GrabPhase;

            Phase mapped = (GrabSlotPhase)slotPhase switch {
                GrabSlotPhase.Hold => Phase.GrabHold,
                GrabSlotPhase.Gorge => Phase.Gorge,
                GrabSlotPhase.Squeeze => Phase.Squeeze,
                GrabSlotPhase.EjectCarry or GrabSlotPhase.EjectLaunched => Phase.Eject,
                _ => phase,
            };

            if (slotPhase >= 1 && mapped != phase) {
                SwitchPhase(context, mapped);
            }
            //服务端已断投(槽清零)而本地还停在抓取轨：跟着回恢复段
            if (slotPhase == 0 && IsGrabTrack(phase)) {
                SwitchPhase(context, Phase.Recover);
            }
        }

        /// <summary>断投：清吞噬声明并转入恢复(权威端)</summary>
        private void AbortGrab(EowStateContext context) {
            context.GrabTargetWho = -1;
            context.GrabPhase = 0;
            context.Npc.netUpdate = true;
            if (IsGrabTrack(phase)) {
                SwitchPhase(context, Phase.Recover);
            }
        }

        /// <summary>相位进入的一次性表现(各端本地各自触发一次)</summary>
        private void EnterPhaseFX(EowStateContext context, Phase next) {
            NPC npc = context.Npc;
            switch (next) {
                case Phase.GrabHold:
                    //咬合：湿裂声+高吼+重击震屏
                    SoundEngine.PlaySound(SoundID.NPCDeath13 with { Pitch = -0.4f, Volume = 1.2f }, npc.Center);
                    EowMotionFX.PlayRoar(npc.Center, 0.45f, 1.15f);
                    EowMotionFX.SpawnAcidBurst(EowSpitBarrageState.MouthPos(npc), 1.8f, -Vector2.UnitY);
                    EowMotionFX.CameraPunch(npc.Center, 8f, 14, "EowDevourBite", -Vector2.UnitY);
                    break;
                case Phase.Squeeze:
                    SoundEngine.PlaySound(SoundID.Zombie13 with { Pitch = -0.75f, Volume = 1.0f, MaxInstances = 2 }, npc.Center);
                    break;
                case Phase.Eject:
                    EowMotionFX.PlayRoar(npc.Center, -0.2f, 1.1f);
                    break;
                case Phase.Whiff:
                    //扑空懊吼
                    EowMotionFX.PlayRoar(npc.Center, -0.05f, 0.95f);
                    break;
            }
        }

        /// <summary>观察同步拍计数变化，驱动本地压缩包络与地表闷震(旁观者可读节拍)</summary>
        private void WatchBeatChanges(EowStateContext context) {
            int beat = context.GrabBeat;
            if (lastSeenBeat < 0) {
                //首帧对齐(含中途加入)，不补爆历史拍
                lastSeenBeat = beat;
                return;
            }
            beatFxTimer++;
            if (beat == lastSeenBeat) {
                return;
            }
            lastSeenBeat = beat;
            beatFxTimer = 0;

            NPC npc = context.Npc;
            //按头部当前X重新探地表(挤压期头会横向游摆，斜坡地形下旧锚点会飘)
            Vector2 surface = EowMotionFX.FindGroundBelow(new Vector2(npc.Center.X, groundY - 600f));
            //地表闷震尘泉：地上的人看见"地底有东西在挤"
            EowMotionFX.SpawnDirtBurst(surface, 1.15f, withSound: false);
            SoundEngine.PlaySound(SoundID.WormDig with { Volume = 1.0f, Pitch = -0.75f, MaxInstances = 3 }, surface);
            SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.8f, Pitch = -0.7f, MaxInstances = 3 }, npc.Center);
            EowMotionFX.CameraPunch(surface, 4.5f, 12, "EowDevourBeat", Vector2.UnitY);
        }
        #endregion

        #region 入土
        private void UpdateDiveIn(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            int side = Math.Sign(npc.Center.X - player.Center.X);
            if (side == 0) {
                side = 1;
            }
            SetMovement(context, new Vector2(player.Center.X + side * 180f, groundY + 880f),
                MathHelper.Lerp(24f, 40f, Math.Min(Timer / 30f, 1f)), 1.15f);
            context.AccelRate = 0.09f;
            context.SlitherStrength = 0.35f;

            if (!entryFired && npc.Center.Y > groundY + 30f) {
                entryFired = true;
                EowMotionFX.SpawnDirtBurst(new Vector2(npc.Center.X, groundY), 1.2f);
                EowMotionFX.CameraPunch(new Vector2(npc.Center.X, groundY), 4f, 11, "EowDevourDive", Vector2.UnitY);
            }

            if (npc.Center.Y > groundY + 520f || Timer > DiveMaxTime) {
                SwitchPhase(context, Phase.Ambush);
            }
        }
        #endregion

        #region 伏击预警
        private void UpdateAmbush(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //锁点与预兆：t=1各端提交，破土帧沿用(预告即承诺)
            if (Timer == 1) {
                lockX = player.Center.X + player.velocity.X * 8f;
                groundY = EowMotionFX.FindGroundBelow(new Vector2(lockX, player.Center.Y)).Y;
                breachFired = false;
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), new Vector2(lockX, groundY), Vector2.Zero,
                        ModContent.ProjectileType<EowDevourOmen>(), 0, 0f, Main.myPlayer, AmbushTime + 2, 0f);
                }
                SoundEngine.PlaySound(SoundID.Zombie13 with { Pitch = -0.5f, Volume = 1.0f }, new Vector2(lockX, groundY));
            }

            //锁点正下方盘桓蓄势，腭光爬升
            float t = Timer / (float)AmbushTime;
            context.MawGlow = t;
            context.SlitherStrength = 0.5f * (1f - t);
            //末段收拢压缩(吸气拍)
            context.Compression = MathHelper.Lerp(1f, 0.8f, t * t);
            SetMovement(context, new Vector2(lockX, groundY + 430f), 24f, 1.6f);

            //震颤爬升(旁观者与目标都读得到)
            if (Timer % 11 == 0 && t > 0.25f) {
                EowMotionFX.CameraPunch(new Vector2(lockX, groundY), 1.2f + t * 3f, 12, "EowDevourOmenRumble");
            }

            if (Timer >= AmbushTime) {
                SwitchPhase(context, Phase.Breach);
            }
        }
        #endregion

        #region 破土吞咬
        private void UpdateBreach(EowStateContext context) {
            NPC npc = context.Npc;

            context.SkipDefaultMovement = true;
            context.MawGlow = 1f;

            //喷发帧：地下就位垂直射出(张口)
            if (Timer == 1) {
                npc.Center = new Vector2(lockX, groundY + 760f);
                npc.velocity = -Vector2.UnitY * BreachSpeed;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.netUpdate = true;
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.35f, Volume = 1.2f }, new Vector2(lockX, groundY));
            }

            //破土表现
            if (!breachFired && npc.Center.Y < groundY) {
                breachFired = true;
                Vector2 breachPoint = new Vector2(lockX, groundY);
                EowMotionFX.SpawnBreachBlast(breachPoint, 1.7f, -Vector2.UnitY);
                EowMotionFX.CameraPunch(breachPoint, 8f, 15, "EowDevourBreach", -Vector2.UnitY);
            }

            //抓取判定(仅权威端)：窗口与可见上冲严格对齐；
            //头部接近地表后才激活，深埋段不抓(洞穴里的玩家看不见预兆盘，被看不见的判定吞掉不公平)
            if (!VaultUtils.isClient && Timer <= GrabWindow && npc.velocity.Y < -10f
                && npc.Center.Y < groundY + 140f) {
                Player caught = FindGrabbablePlayer(npc);
                if (caught != null) {
                    context.GrabTargetWho = caught.whoAmI;
                    context.GrabPhase = (int)GrabSlotPhase.Hold;
                    npc.netUpdate = true;
                    SwitchPhase(context, Phase.GrabHold);
                    return;
                }
            }

            //上冲末段拱弧(为扑空回摔起势)
            if (breachFired && Timer > 14) {
                npc.velocity.Y += 1.3f;
            }
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            //窗口耗尽或冲势衰竭→扑空
            if (Timer > BreachMaxTime || (breachFired && npc.velocity.Y > -6f)) {
                SwitchPhase(context, Phase.Whiff);
            }
        }

        /// <summary>头部本帧扫掠线段附近可吞的玩家(最近优先)</summary>
        private Player FindGrabbablePlayer(NPC npc) {
            Vector2 from = npc.oldPosition + npc.Size / 2f;
            Vector2 to = npc.Center;
            Player best = null;
            float bestDist = float.MaxValue;
            foreach (Player p in Main.ActivePlayers) {
                if (!p.Alives() || p.shimmering) {
                    continue;
                }
                float dist = DistanceToSegment(p.Center, from, to);
                if (dist < GrabRadius && dist < bestDist) {
                    bestDist = dist;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>点到线段距离</summary>
        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b) {
            Vector2 ab = b - a;
            float lenSq = ab.LengthSquared();
            if (lenSq < 0.001f) {
                return Vector2.Distance(point, a);
            }
            float t = MathHelper.Clamp(Vector2.Dot(point - a, ab) / lenSq, 0f, 1f);
            return Vector2.Distance(point, a + ab * t);
        }
        #endregion

        #region 咬合顿帧
        private void UpdateGrabHold(EowStateContext context) {
            NPC npc = context.Npc;

            context.SkipDefaultMovement = true;
            context.MawGlow = 1f;
            //急刹悬停：吞下的一瞬间世界慢下来
            npc.velocity *= 0.55f;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            context.Compression = 0.82f;

            //抓取轨相位推进只归权威端，客户端全靠槽仲裁(防追帧超车振荡)
            if (Timer >= HoldTime && !VaultUtils.isClient) {
                context.GrabPhase = (int)GrabSlotPhase.Gorge;
                npc.netUpdate = true;
                SwitchPhase(context, Phase.Gorge);
            }
        }
        #endregion

        #region 携人入地
        private void UpdateGorge(EowStateContext context) {
            NPC npc = context.Npc;

            context.SkipDefaultMovement = true;
            context.MawGlow = 0.8f;

            //调头下潜：航向压向正下，速度爬升
            float heading = npc.velocity.LengthSquared() > 0.1f ? npc.velocity.ToRotation() : MathHelper.PiOver2;
            heading = heading.AngleTowards(MathHelper.PiOver2, 0.115f);
            float speed = MathHelper.Lerp(Math.Max(npc.velocity.Length(), 8f), 44f, 0.075f);
            npc.velocity = heading.ToRotationVector2() * speed;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            //携人穿地：入地闷爆(受害者镜头此刻转暗，由 EowDevourPlayer 驱动)
            if (!gorgeEntryFired && npc.Center.Y > groundY + 20f) {
                gorgeEntryFired = true;
                EowMotionFX.SpawnDirtBurst(new Vector2(npc.Center.X, groundY), 1.6f);
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 1.2f, Pitch = -0.6f }, new Vector2(npc.Center.X, groundY));
                EowMotionFX.CameraPunch(new Vector2(npc.Center.X, groundY), 6f, 13, "EowDevourGorge", Vector2.UnitY);
            }

            if ((npc.Center.Y > groundY + 560f || Timer > GorgeMaxTime) && !VaultUtils.isClient) {
                context.GrabPhase = (int)GrabSlotPhase.Squeeze;
                npc.netUpdate = true;
                SwitchPhase(context, Phase.Squeeze);
            }
        }
        #endregion

        #region 腹内挤压
        private void UpdateSqueeze(EowStateContext context) {
            NPC npc = context.Npc;

            context.SkipDefaultMovement = true;
            context.MawGlow = 0.45f;
            context.MiasmaLevel = 0.35f;

            //地底缓速蠕动：横向游摆+深度保持
            float sway = (float)Math.Sin(Timer * 0.045f) * 5.5f;
            float targetDepth = groundY + 540f;
            float vy = MathHelper.Clamp((targetDepth - npc.Center.Y) * 0.02f, -3f, 3f);
            npc.velocity = Vector2.Lerp(npc.velocity, new Vector2(sway, vy), 0.12f);
            if (npc.velocity.LengthSquared() > 0.1f) {
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            }

            //挤压拍：权威端按表递增事件计数(受害端消费伤害)
            if (!VaultUtils.isClient) {
                for (int i = 0; i < BeatFrames.Length; i++) {
                    if (Timer == BeatFrames[i]) {
                        context.GrabBeat++;
                        npc.netUpdate = true;
                        break;
                    }
                }
            }

            //压缩包络锚定观察到的拍事件：全链手风琴狠夹一次
            if (beatFxTimer < 24) {
                float bump = (float)Math.Sin(MathHelper.Pi * beatFxTimer / 24f);
                context.Compression = 1f - 0.38f * bump;
                context.PulseKind = 1;
                context.PulsePhase = MathHelper.Clamp(beatFxTimer / 24f, 0f, 1f);
            }
            else {
                context.Compression = 0.94f;
            }

            if (Timer >= SqueezeTime && !VaultUtils.isClient) {
                context.GrabPhase = (int)GrabSlotPhase.EjectCarry;
                npc.netUpdate = true;
                SwitchPhase(context, Phase.Eject);
            }
        }
        #endregion

        #region 破土喷出
        private void UpdateEject(EowStateContext context) {
            NPC npc = context.Npc;

            context.SkipDefaultMovement = true;
            context.MawGlow = 1f;

            //相位入场各端自行提交喷出地表线(头当前X上方探地)，一次提交后沿用
            if (ejectSurfaceY == 0f) {
                ejectSurfaceY = EowMotionFX.FindGroundBelow(new Vector2(npc.Center.X, groundY - 700f)).Y;
            }

            //起冲帧：垂直上射
            if (Timer == 1) {
                npc.velocity = -Vector2.UnitY * EjectSpeed;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.1f, Volume = 1.15f }, npc.Center);
            }

            //破土帧：喷人+巨爆；权威端在此宣告释放(受害端读到后自弹射)
            if (!ejectBreachFired && npc.Center.Y < ejectSurfaceY) {
                ejectBreachFired = true;
                Vector2 breachPoint = new Vector2(npc.Center.X, ejectSurfaceY);
                EowMotionFX.SpawnBreachBlast(breachPoint, 2.2f, -Vector2.UnitY);
                EowMotionFX.SpawnAcidBurst(EowSpitBarrageState.MouthPos(npc), 2.2f, -Vector2.UnitY);
                EowMotionFX.CameraPunch(breachPoint, 10f, 18, "EowDevourEject", -Vector2.UnitY);
                if (!VaultUtils.isClient) {
                    context.GrabTargetWho = -1;
                    context.GrabPhase = (int)GrabSlotPhase.EjectLaunched;
                    npc.netUpdate = true;
                }
            }

            //破土后拱弧回摔
            if (ejectBreachFired && Timer > 12) {
                npc.velocity.Y += 1.5f;
                npc.velocity.X += Math.Sign(npc.velocity.X == 0 ? 1f : npc.velocity.X) * 0.24f;
                if (npc.velocity.Length() > EjectSpeed) {
                    npc.velocity = npc.velocity.SafeNormalize(Vector2.UnitY) * EjectSpeed;
                }
            }
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            if (Timer > EjectMaxTime && !VaultUtils.isClient) {
                //极端卡地形没破土也要放人；进入恢复段清相位槽，客户端随槽归位
                if (context.GrabTargetWho >= 0) {
                    context.GrabTargetWho = -1;
                }
                context.GrabPhase = 0;
                npc.netUpdate = true;
                SwitchPhase(context, Phase.Recover);
            }
        }
        #endregion

        #region 扑空惩罚
        private bool UpdateWhiff(EowStateContext context) {
            NPC npc = context.Npc;

            context.SkipDefaultMovement = true;
            context.MawGlow = MathHelper.Clamp(1f - Timer / 20f, 0f, 1f);

            //重力回摔：整段暴露在外，留给玩家自由输出
            npc.velocity.Y += 1.5f;
            npc.velocity.X *= 0.995f;
            if (npc.velocity.Length() > BreachSpeed) {
                npc.velocity = npc.velocity.SafeNormalize(Vector2.UnitY) * BreachSpeed;
            }
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            //摔回地面尘爆
            if (!whiffReentryFired && npc.velocity.Y > 0f && npc.Center.Y > groundY + 40f) {
                whiffReentryFired = true;
                EowMotionFX.SpawnDirtBurst(new Vector2(npc.Center.X, groundY), 1.3f);
                EowMotionFX.CameraPunch(new Vector2(npc.Center.X, groundY), 5f, 12, "EowDevourWhiff", Vector2.UnitY);
            }

            return Timer > WhiffTime;
        }
        #endregion

        #region 恢复
        private bool UpdateRecover(EowStateContext context) {
            NPC npc = context.Npc;

            context.SkipDefaultMovement = false;
            context.SlitherStrength = 0.5f;
            context.MawGlow = 0f;

            Vector2 anchor = context.Target.Alives()
                ? new Vector2(context.Target.Center.X, groundY + 430f)
                : npc.Center + new Vector2(0f, 200f);
            SetMovement(context, anchor, 20f, 1.1f);

            return Timer > RecoverTime;
        }
        #endregion

        public override void OnExit(EowStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.AccelRate = 0.07f;
            context.Npc.damage = context.Npc.defDamage;
            //任何退出路径都放人：受害端读到槽清零即自解锁
            if (!VaultUtils.isClient) {
                context.GrabTargetWho = -1;
                context.GrabPhase = 0;
                context.GrabBeat = 0;
                context.Npc.netUpdate = true;
            }
        }
    }
}
