using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.Cinematics;
using InnoVault.GameSystem;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common
{
    /// <summary>
    /// 钳形投技合击：双眼占据玩家两侧同一轴线，锁线蓄力后对向冲刺，
    /// 在交点交扣夹住玩家→激光眼光绳束缚→魔焰眼绕环三轮喷灼→双眼反向弹射甩出收尾；
    /// 扑空则交错穿过进入硬直惩罚窗。
    /// 判定与节拍推进仅服务端(魔焰眼为指挥)，节拍经 override ai[8..10] 同步；
    /// 被抓玩家的位移与锁定由其本地 <see cref="TwinsGrabPerformancePlayer"/> 施加。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.TwinsPincerGrab, typeof(TwinsStateContext))]
    internal class TwinsPincerGrabState : TwinsStateBase
    {
        public override string StateName => "TwinsPincerGrab";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.TwinsPincerGrab;

        #region 节拍与时长常量

        //节拍值，经 ai[9] 同步；-1 扑空 0 未定 1~6 见名
        internal const int BeatWhiff = -1;
        internal const int BeatNone = 0;
        internal const int BeatClamp = 1;
        internal const int BeatBind = 2;
        internal const int BeatFlames = 3;
        internal const int BeatEjectCharge = 4;
        internal const int BeatEject = 5;
        internal const int BeatRecover = 6;

        private int GatherPhase => Context.IsDeathMode ? 40 : 46;
        private int LockPhase => Context.IsDeathMode ? 34 : 40;
        private const int ClosePhaseMax = 16;
        private const int WhiffRecover = 44;
        private const int ClampTime = 10;
        private const int BindTime = 26;
        private const int FlameRoundTime = 26;
        private const int FlameRounds = 3;
        private const int EjectChargeTime = 10;
        private const int EjectTime = 16;
        private const int RecoverTime = 30;
        private const int MaxPartnerWait = 120;
        //全阶段+等待的硬性保底
        private const int HardTimeout = 560;

        private const float GatherDistance = 640f;
        private const float ClampOffset = 74f;
        private const float CatchRadius = 120f;
        private const float InterlockRadius = 92f;
        private const float DashContactMinSpeed = 24f;
        //被抓者离交点过远(传送等)即断投
        private const float AbortDistance = 1600f;
        private const float OrbitRadius = 270f;
        //救援阀：双眼合计掉血超过合计上限的此比例即提前弹射
        private const float RescueDamageRatio = 0.06f;
        //慈悲阀：被抓者血量低于上限此比例则跳过剩余喷灼
        private const float MercyLifeRatio = 0.2f;

        private float CloseSpeed => Context.IsDeathMode ? 56f : 52f;

        #endregion

        #region 实例字段(全部本地，不跨端)

        private TwinsStateContext Context;
        private int comboStep;
        private int partnerWait;
        //本端估算的交扣点与轴线角，节拍到来后以同步值校准
        private Vector2 clampLocal;
        private float angleLocal;
        private bool launched;
        private int whiffTick;
        //节拍本地计时，节拍变化时清零
        private int beatTick;
        private int lastSeenBeat;
        //魔焰绕环角
        private float orbitAngle;

        #endregion

        public TwinsPincerGrabState() : this(0) {
        }

        public TwinsPincerGrabState(int currentComboStep) {
            comboStep = currentComboStep;
        }

        #region 同步数据访问

        /// <summary>当前节拍：客户端读同步槽，权威端读静态记录</summary>
        private int CurrentBeat => VaultUtils.isClient
            ? (int)Context.Ai[9]
            : TwinsStateContext.PincerBeat;

        /// <summary>被夹玩家，无效为 null</summary>
        private Player GrabbedPlayer {
            get {
                int idx = VaultUtils.isClient
                    ? (int)Context.Ai[8] - 1
                    : TwinsStateContext.PincerGrabbedPlayer;
                if (idx < 0 || idx >= Main.maxPlayers) {
                    return null;
                }
                return Main.player[idx];
            }
        }

        /// <summary>
        /// 从任一只处于投技态的眼读取同步的投技数据，供表演层/光绳/运镜使用，全端可用
        /// </summary>
        internal static bool TryGetGrabData(NPC eye, out int grabbedPlayer, out int beat, out float lineAngle) {
            grabbedPlayer = -1;
            beat = BeatNone;
            lineAngle = 0f;
            if (eye == null || !eye.active) {
                return false;
            }
            if (eye.type != NPCID.Spazmatism && eye.type != NPCID.Retinazer) {
                return false;
            }
            if ((int)eye.ai[1] != (int)TwinsStateIndex.TwinsPincerGrab) {
                return false;
            }
            if (!eye.TryGetOverride(out Dictionary<Type, NPCOverride> overrides)) {
                return false;
            }
            if (!overrides.TryGetValue(typeof(TwinsAIController), out NPCOverride ov)
                && !overrides.TryGetValue(typeof(RetinazerAIController), out ov)) {
                return false;
            }
            if (ov?.ai == null || ov.ai.Length < 11) {
                return false;
            }
            grabbedPlayer = (int)ov.ai[8] - 1;
            beat = (int)ov.ai[9];
            lineAngle = ov.ai[10];
            return true;
        }

        #endregion

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            partnerWait = 0;
            launched = false;
            whiffTick = 0;
            beatTick = 0;
            lastSeenBeat = BeatNone;
            orbitAngle = 0f;
            clampLocal = context.Target?.Center ?? context.Npc.Center;
            angleLocal = 0f;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //搭档失效→投技崩解，立即释放并退出
            NPC partner = TwinsStateContext.GetPartnerNpc(npc.type);
            if (!partner.Alives()) {
                if (!VaultUtils.isClient) {
                    EndPincer(stampCooldown: true, wasWhiff: false);
                }
                TwinsStateContext.ClearComboSignal();
                return GetExitState();
            }

            //节拍期间搭档被强制切走(死亡演出等)→立即放人退出，不留单颚
            if (!VaultUtils.isClient && CurrentBeat >= BeatClamp && CurrentBeat < BeatRecover
                && (int)partner.ai[1] != (int)TwinsStateIndex.TwinsPincerGrab) {
                EndPincer(stampCooldown: true, wasWhiff: false);
                TwinsStateContext.ClearComboSignal();
                return GetExitState();
            }

            Timer++;

            //保底超时，任何卡死都强制脱离
            if (Timer >= HardTimeout) {
                if (!VaultUtils.isClient) {
                    EndPincer(stampCooldown: true, wasWhiff: false);
                }
                return GetExitState();
            }

            //权威端镜像静态记录到本眼同步槽
            MirrorSyncSlots(npc);

            int beat = CurrentBeat;

            //共享记录已被搭档收尾清零(双眼同拍退出的次序保护)→自己立即退出，
            //冷却戳由先退者盖，这里不重复盖以免覆盖扑空标记
            if (beat == BeatNone && lastSeenBeat != BeatNone) {
                TwinsStateContext.ClearComboSignal();
                return GetExitState();
            }

            //节拍变化时校准本地锚点并清零节拍计时
            if (beat != lastSeenBeat) {
                OnBeatChanged(npc, partner, beat);
                lastSeenBeat = beat;
                beatTick = 0;
            }
            beatTick++;

            //权威端周期校验被抓者有效性
            if (!VaultUtils.isClient && Context.IsSpazmatism) {
                ConductorValidate(npc, partner);
            }

            if (beat == BeatWhiff) {
                return UpdateWhiff(npc, player);
            }

            if (beat == BeatNone) {
                UpdatePreGrab(npc, partner, player);
                return null;
            }

            UpdateGrabBeats(npc, partner, beat);

            //恢复拍走完→自然退出
            if (beat == BeatRecover && beatTick >= RecoverTime) {
                if (!VaultUtils.isClient) {
                    EndPincer(stampCooldown: true, wasWhiff: false);
                }
                return GetExitState();
            }

            return null;
        }

        #region 抓取前：集合→锁线→对冲

        private void UpdatePreGrab(NPC npc, NPC partner, Player player) {
            if (Timer <= GatherPhase) {
                ExecuteGather(npc, partner, player);

                //集合末就绪，等双方同拍(镜像交叉冲刺的等待协议)
                if (Timer == GatherPhase) {
                    TwinsStateContext.MarkComboReady(Context.IsSpazmatism);
                    if (!TwinsStateContext.BothComboReady && partnerWait < MaxPartnerWait) {
                        Timer--;
                        partnerWait++;
                    }
                    else if (!TwinsStateContext.BothComboReady && !VaultUtils.isClient) {
                        //等满搭档仍未进态→当作扑空放弃，避免单颚钳形
                        TwinsStateContext.PincerBeat = BeatWhiff;
                    }
                }
            }
            else if (Timer <= GatherPhase + LockPhase) {
                ExecuteLock(npc, partner, player);
            }
            else {
                ExecuteClose(npc, partner);
            }
        }

        /// <summary>集合：双眼各占轴线一端，魔焰按相对位取侧，激光取对侧</summary>
        private void ExecuteGather(NPC npc, NPC partner, Player player) {
            float progress = Timer / (float)GatherPhase;

            //以魔焰眼相对玩家的横向位定钳形两侧，双端从同步位置推得一致结论
            NPC spazEye = Context.IsSpazmatism ? npc : partner;
            float spazSide = spazEye.Center.X < player.Center.X ? -1f : 1f;
            float mySide = Context.IsSpazmatism ? spazSide : -spazSide;

            Vector2 targetPos = player.Center + new Vector2(mySide * GatherDistance, 0f);
            TwinsMotion.SpringHover(npc, targetPos, 0.024f, 0.11f);
            FaceTarget(npc, player.Center);

            Context.SetChargeState(14, progress * 0.35f);

            if (Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.35f, Volume = 0.95f }, npc.Center);
            }
        }

        /// <summary>锁线：刹车悬停，持续追瞄玩家，末 30% 冻结轴线并绷紧颤抖</summary>
        private void ExecuteLock(NPC npc, NPC partner, Player player) {
            int phaseTimer = Timer - GatherPhase;
            float progress = phaseTimer / (float)LockPhase;

            npc.velocity *= 0.85f;

            NPC spazEye = Context.IsSpazmatism ? npc : partner;

            //前 70% 持续修正交点与轴线，之后冻结
            if (progress <= 0.7f) {
                clampLocal = player.Center;
                angleLocal = (clampLocal - spazEye.Center).ToRotation();
                //指挥写共享记录，供搭档与抓取判定使用；服务端搭档直接镜像共享记录保证共线
                if (!VaultUtils.isClient) {
                    if (Context.IsSpazmatism) {
                        TwinsStateContext.PincerClampPoint = clampLocal;
                        TwinsStateContext.PincerLineAngle = angleLocal;
                    }
                    else if (TwinsStateContext.PincerClampPoint != Vector2.Zero) {
                        clampLocal = TwinsStateContext.PincerClampPoint;
                        angleLocal = TwinsStateContext.PincerLineAngle;
                    }
                }
            }
            else if (!VaultUtils.isServer) {
                //锁定后绷紧颤抖(纯本地表现)
                npc.position += Main.rand.NextVector2Circular(1.6f, 1.6f);
            }

            //把自己弹簧修正到轴线端点上，保证两颚共线
            Vector2 lineDir = angleLocal.ToRotationVector2();
            Vector2 myEnd = Context.IsSpazmatism
                ? clampLocal - lineDir * GatherDistance
                : clampLocal + lineDir * GatherDistance;
            TwinsMotion.SpringHover(npc, myEnd, 0.03f, 0.16f);

            //面向交点
            npc.rotation = ((clampLocal - npc.Center).SafeNormalize(Vector2.UnitY)).ToRotation() - MathHelper.PiOver2;

            Context.SetChargeState(14, 0.35f + progress * 0.65f);

            //能量内聚
            if (phaseTimer % 2 == 0) {
                TwinsMotion.ChargeGatherFX(npc.Center, Context.IsSpazmatism, progress, 90f);
            }

            //锁定咔哒与末段咆哮
            if (phaseTimer == (int)(LockPhase * 0.7f) && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item93 with { Pitch = 0.4f, Volume = 0.85f }, npc.Center);
            }
            if (phaseTimer == LockPhase - 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f }, npc.Center);
            }
        }

        /// <summary>对冲：双眼同拍向交点猛扑，指挥逐帧判交扣</summary>
        private void ExecuteClose(NPC npc, NPC partner) {
            Context.ResetChargeState();

            if (!launched) {
                launched = true;
                Vector2 dir = (clampLocal - npc.Center).SafeNormalize(Vector2.UnitY);
                TwinsMotion.DashLaunch(npc, dir, CloseSpeed, Context.IsSpazmatism, 1.15f);
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
            }

            //伤害窗口精确对齐冲刺速度
            EnableContactDamageIfFast(npc, DashContactMinSpeed);
            FaceVelocity(npc);
            Context.PushDashVisuals(1f, 1f);

            //冲刺拖尾
            if (!VaultUtils.isServer && Timer % 2 == 0) {
                PRTLoader.NewParticle<PRT_TwinsSpark>(
                    npc.Center - npc.velocity.SafeNormalize(Vector2.Zero) * 30f + Main.rand.NextVector2Circular(10, 10),
                    -npc.velocity * 0.12f, Color.White, Main.rand.NextFloat(1f, 1.6f))?
                    .Configure(15, Context.IsSpazmatism ? 1 : 0);
            }

            //越过交点则强刹，避免扑空时飞远
            Vector2 toClamp = clampLocal - npc.Center;
            if (Vector2.Dot(toClamp, npc.velocity) < 0f && toClamp.Length() > 40f) {
                npc.velocity *= 0.7f;
            }

            //指挥端判定：双眼皆近交点→抓取判定；超时→扑空
            if (!VaultUtils.isClient && Context.IsSpazmatism && TwinsStateContext.PincerBeat == BeatNone) {
                int closeTimer = Timer - GatherPhase - LockPhase;
                Vector2 c = TwinsStateContext.PincerClampPoint;
                bool interlocked = Vector2.Distance(npc.Center, c) < InterlockRadius
                    && Vector2.Distance(partner.Center, c) < InterlockRadius;
                if (interlocked) {
                    ResolveCatch(npc, partner, c);
                }
                else if (closeTimer >= ClosePhaseMax) {
                    TwinsStateContext.PincerBeat = BeatWhiff;
                }
            }
        }

        /// <summary>交扣瞬间的抓取判定，命中最近的玩家，否则扑空</summary>
        private void ResolveCatch(NPC npc, NPC partner, Vector2 clampPoint) {
            Player caught = null;
            float bestDist = float.MaxValue;
            foreach (Player candidate in Main.ActivePlayers) {
                if (!candidate.Alives()) {
                    continue;
                }
                Rectangle hb = candidate.Hitbox;
                hb.Inflate(32, 32);
                Vector2 closest = new(
                    MathHelper.Clamp(clampPoint.X, hb.Left, hb.Right),
                    MathHelper.Clamp(clampPoint.Y, hb.Top, hb.Bottom));
                float dist = Vector2.Distance(closest, clampPoint);
                if (dist <= CatchRadius && dist < bestDist) {
                    bestDist = dist;
                    caught = candidate;
                }
            }

            if (caught == null) {
                TwinsStateContext.PincerBeat = BeatWhiff;
                return;
            }

            TwinsStateContext.PincerGrabbedPlayer = caught.whoAmI;
            TwinsStateContext.PincerBeat = BeatClamp;
            TwinsStateContext.PincerClampPoint = clampPoint;
            TwinsStateContext.PincerEyesLifeAtClamp = npc.life + partner.life;

            //双颚立即交扣定位，杜绝穿过一帧的错位
            Vector2 lineDir = TwinsStateContext.PincerLineAngle.ToRotationVector2();
            npc.Center = clampPoint - lineDir * ClampOffset;
            partner.Center = clampPoint + lineDir * ClampOffset;
            npc.velocity = Vector2.Zero;
            partner.velocity = Vector2.Zero;
            npc.netUpdate = true;
            partner.netUpdate = true;
        }

        #endregion

        #region 扑空硬直

        private ITwinsState UpdateWhiff(NPC npc, Player player) {
            whiffTick++;
            Context.ResetChargeState();

            //交错穿过后余威渐止：前几帧仍带伤害，随后关伤减速
            if (whiffTick <= 8) {
                EnableContactDamageIfFast(npc, DashContactMinSpeed);
            }
            else {
                DisableContactDamage(npc);
                npc.velocity *= 0.9f;
            }

            if (whiffTick == 9 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.5f, Pitch = -0.35f }, npc.Center);
            }

            //硬直中缓慢转向玩家，姿态上的"懊恼"
            if (player.Alives()) {
                TwinsMotion.BrakeAndWhip(npc, player.Center, 0.94f, 0.06f);
            }

            if (whiffTick >= WhiffRecover) {
                if (!VaultUtils.isClient) {
                    EndPincer(stampCooldown: true, wasWhiff: true);
                }
                return GetExitState();
            }
            return null;
        }

        #endregion

        #region 抓取节拍推进

        /// <summary>节拍切换钩子：校准锚点、放一次性演出</summary>
        private void OnBeatChanged(NPC npc, NPC partner, int beat) {
            //权威端直接取共享记录
            if (!VaultUtils.isClient && beat >= BeatClamp) {
                clampLocal = TwinsStateContext.PincerClampPoint;
                angleLocal = TwinsStateContext.PincerLineAngle;
            }
            //客户端在夹合瞬间用自身眼位反推交扣点：自己的同步包原子携带
            //snap 后位置与节拍槽，不依赖搭档包到达时序
            else if (VaultUtils.isClient && beat == BeatClamp) {
                angleLocal = Context.Ai[10];
                Vector2 dir = angleLocal.ToRotationVector2();
                clampLocal = Context.IsSpazmatism
                    ? npc.Center + dir * ClampOffset
                    : npc.Center - dir * ClampOffset;
            }
            //后入场客户端(从无节拍直接看到束缚后的节拍)：以持绳激光眼的停位反推交扣点
            else if (VaultUtils.isClient && lastSeenBeat == BeatNone
                && beat > BeatClamp && beat < BeatRecover) {
                angleLocal = Context.Ai[10];
                NPC retinEye = Context.IsSpazmatism ? partner : npc;
                clampLocal = retinEye.Center - angleLocal.ToRotationVector2() * 150f;
            }

            switch (beat) {
                case BeatClamp:
                    //交扣顿帧：金铁交鸣+电闪+环形冲击
                    DisableContactDamage(npc);
                    if (Context.IsSpazmatism && !VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 1.2f, Pitch = -0.4f }, clampLocal);
                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = -0.2f }, clampLocal);
                        PRTLoader.NewParticle<PRT_DWave>(clampLocal, Vector2.Zero, Color.White * 0.85f, 0.16f)?
                            .Configure(Vector2.One, 0f, 1.2f, 14);
                        PRTLoader.NewParticle<PRT_DWave>(clampLocal, Vector2.Zero, TwinsMotion.SpazColor, 0.22f)?
                            .Configure(Vector2.One, 0f, 1.6f, 18);
                        for (int i = 0; i < 14; i++) {
                            PRTLoader.NewParticle<PRT_TwinsSpark>(clampLocal, VaultUtils.RandVr(4, 12),
                                Color.White, Main.rand.NextFloat(1.1f, 1.9f))?.Configure(18, i % 2);
                        }
                        GrabShake(clampLocal, 8f, 12);
                    }
                    break;
                case BeatBind:
                    //激光眼放光绳(权威端只放一次，由激光眼实例负责)
                    if (!VaultUtils.isClient && !Context.IsSpazmatism) {
                        int duration = BindTime + FlameRoundTime * FlameRounds + EjectChargeTime + 20;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                            ModContent.ProjectileType<PincerBindTether>(), 0, 0f, Main.myPlayer,
                            npc.whoAmI, TwinsStateContext.PincerGrabbedPlayer, duration);
                    }
                    break;
                case BeatEjectCharge:
                    if (Context.IsSpazmatism && !VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.6f, Volume = 0.9f }, clampLocal);
                    }
                    break;
                case BeatEject:
                    //反向弹射：双眼沿轴线相背炸开
                    if (!VaultUtils.isServer) {
                        Vector2 lineDir = angleLocal.ToRotationVector2();
                        Vector2 outDir = Context.IsSpazmatism ? -lineDir : lineDir;
                        TwinsMotion.SonicBoom(npc.Center, outDir, Context.IsSpazmatism, 1.1f);
                        if (Context.IsSpazmatism) {
                            PRTLoader.NewParticle<PRT_DWave>(clampLocal, Vector2.Zero, Color.White * 0.9f, 0.2f)?
                                .Configure(Vector2.One, 0f, 1.8f, 18);
                            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 0.9f }, clampLocal);
                            GrabShake(clampLocal, 10f, 14);
                        }
                    }
                    break;
            }
        }

        /// <summary>指挥端逐帧校验：被抓者失效/远离、救援阀、慈悲阀</summary>
        private void ConductorValidate(NPC npc, NPC partner) {
            int beat = TwinsStateContext.PincerBeat;
            if (beat < BeatClamp || beat >= BeatRecover) {
                return;
            }

            Player grabbed = GrabbedPlayer;
            //被抓者死亡/掉线/被传送→直接进入恢复拍(无弹射)
            if (grabbed == null || !grabbed.active || grabbed.dead
                || Vector2.Distance(grabbed.Center, TwinsStateContext.PincerClampPoint) > AbortDistance) {
                TwinsStateContext.PincerBeat = BeatRecover;
                return;
            }

            if (beat == BeatFlames) {
                //救援阀：束缚期间双眼被同伴打掉足量血→提前弹射
                int combinedLife = npc.life + partner.life;
                int combinedMax = npc.lifeMax + partner.lifeMax;
                if (TwinsStateContext.PincerEyesLifeAtClamp - combinedLife > combinedMax * RescueDamageRatio) {
                    TwinsStateContext.PincerBeat = BeatEjectCharge;
                    return;
                }
                //慈悲阀：被抓者濒死则跳过剩余喷灼
                if (grabbed.statLife < grabbed.statLifeMax2 * MercyLifeRatio) {
                    TwinsStateContext.PincerBeat = BeatEjectCharge;
                }
            }
        }

        /// <summary>抓取期间的双眼运动与攻击节拍</summary>
        private void UpdateGrabBeats(NPC npc, NPC partner, int beat) {
            Context.ResetChargeState();
            DisableContactDamage(npc);

            Vector2 lineDir = angleLocal.ToRotationVector2();
            //魔焰在轴线负侧，激光在正侧
            Vector2 myHoldPos = Context.IsSpazmatism
                ? clampLocal - lineDir * ClampOffset
                : clampLocal + lineDir * ClampOffset;

            switch (beat) {
                case BeatClamp:
                    //顿帧：钉死在交扣位，面向猎物
                    TwinsMotion.SpringHover(npc, myHoldPos, 0.4f, 0.5f);
                    FaceTarget(npc, clampLocal);
                    break;

                case BeatBind:
                    if (Context.IsSpazmatism) {
                        //魔焰上浮脱离，准备入轨
                        Vector2 orbitEntry = clampLocal + (angleLocal + MathHelper.Pi).ToRotationVector2() * OrbitRadius;
                        TwinsMotion.SpringHover(npc, orbitEntry, 0.045f, 0.16f);
                        orbitAngle = angleLocal + MathHelper.Pi;
                        FaceTarget(npc, clampLocal);
                    }
                    else {
                        //激光后撤放绳，保持束缚姿态
                        Vector2 anchorPos = clampLocal + lineDir * 150f;
                        TwinsMotion.SpringHover(npc, anchorPos, 0.06f, 0.2f);
                        FaceTarget(npc, clampLocal);
                    }
                    break;

                case BeatFlames:
                    UpdateFlameOrbit(npc, beat);
                    break;

                case BeatEjectCharge: {
                    //蓄势内压：双颚收回轴线并向交点挤压，颤抖蓄力
                    float squeeze = VaultUtils.EaseOutCubic(MathHelper.Clamp(beatTick / (float)EjectChargeTime, 0f, 1f)) * 26f;
                    Vector2 squeezePos = Context.IsSpazmatism
                        ? clampLocal - lineDir * (ClampOffset + 30f - squeeze)
                        : clampLocal + lineDir * (ClampOffset + 76f - squeeze);
                    TwinsMotion.SpringHover(npc, squeezePos, 0.25f, 0.4f);
                    if (!VaultUtils.isServer) {
                        npc.position += Main.rand.NextVector2Circular(1.4f, 1.4f);
                    }
                    FaceTarget(npc, clampLocal);
                    break;
                }

                case BeatEject:
                    //相背弹射，之后拖减
                    if (beatTick == 1) {
                        Vector2 outDir = Context.IsSpazmatism ? -lineDir : lineDir;
                        npc.velocity = outDir * 46f;
                        if (!VaultUtils.isClient) {
                            npc.netUpdate = true;
                        }
                    }
                    if (beatTick > 6) {
                        npc.velocity *= 0.86f;
                    }
                    FaceVelocity(npc);
                    Context.PushDashVisuals(0.8f, 0.9f);
                    break;

                case BeatRecover:
                    //恢复拍：喘息漂移
                    npc.velocity *= 0.92f;
                    if (Context.Target != null) {
                        FaceTarget(npc, Context.Target.Center);
                    }
                    //排气烟
                    if (!VaultUtils.isServer && beatTick % 6 == 0) {
                        PRTLoader.NewParticle<PRT_TwinsSpark>(
                            npc.Center + Main.rand.NextVector2Circular(24f, 24f),
                            -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.5f),
                            Color.White, Main.rand.NextFloat(0.6f, 1f))?
                            .Configure(14, Context.IsSpazmatism ? 1 : 0);
                    }
                    break;
            }

            //权威节拍推进(指挥独占)
            if (!VaultUtils.isClient && Context.IsSpazmatism) {
                AdvanceBeatSchedule();
            }
        }

        /// <summary>魔焰绕环三轮喷灼；激光持绳压阵</summary>
        private void UpdateFlameOrbit(NPC npc, int beat) {
            int roundTick = beatTick % FlameRoundTime;

            if (Context.IsSpazmatism) {
                //绕交扣点匀速旋进，火力窗内略提速
                float angSpeed = 0.052f + (roundTick is >= 8 and < 18 ? 0.02f : 0f);
                orbitAngle += angSpeed;
                Vector2 orbitPos = clampLocal + orbitAngle.ToRotationVector2() * OrbitRadius;
                TwinsMotion.SpringHover(npc, orbitPos, 0.09f, 0.26f, 46f);
                FaceTarget(npc, clampLocal);
                Context.PushDashVisuals(0.35f, 0.45f);

                //火力窗：每轮 8~18 帧向交点喷三舌火(权威端)
                if (roundTick is >= 8 and < 18 && roundTick % 2 == 0) {
                    Vector2 fireDir = (clampLocal - npc.Center).SafeNormalize(Vector2.UnitY);
                    if (!VaultUtils.isClient) {
                        Vector2 fireVel = fireDir.RotatedBy(Main.rand.NextFloat(-0.14f, 0.14f))
                            * Main.rand.NextFloat(9.5f, 11.5f);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + fireDir * 40f,
                            fireVel, ModContent.ProjectileType<CursedFlameJet>(), 24, 0f, Main.myPlayer);
                    }
                    if (!VaultUtils.isServer) {
                        if (roundTick == 8) {
                            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.85f }, npc.Center);
                        }
                        PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center + fireDir * 44f,
                            fireDir * 4f + Main.rand.NextVector2Circular(1f, 1f),
                            Color.White, Main.rand.NextFloat(0.9f, 1.4f))?.Configure(12, 1);
                    }
                }
            }
            else {
                //激光眼持绳：轻微呼吸浮动+每轮开火时的后坐脉冲
                Vector2 lineDir = angleLocal.ToRotationVector2();
                Vector2 anchorPos = clampLocal + lineDir * 150f + TwinsMotion.BreathingOffset(4.2f, 8f);
                TwinsMotion.SpringHover(npc, anchorPos, 0.05f, 0.18f);
                FaceTarget(npc, clampLocal);
                if (roundTick == 8) {
                    npc.velocity += lineDir * 2.6f;
                    if (!VaultUtils.isServer) {
                        PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center, -lineDir * 3f,
                            Color.White, 1.1f)?.Configure(12, 0);
                    }
                }
            }
        }

        /// <summary>指挥端按本地节拍计时推进节拍表</summary>
        private void AdvanceBeatSchedule() {
            int beat = TwinsStateContext.PincerBeat;
            switch (beat) {
                case BeatClamp when beatTick >= ClampTime:
                    TwinsStateContext.PincerBeat = BeatBind;
                    break;
                case BeatBind when beatTick >= BindTime:
                    TwinsStateContext.PincerBeat = BeatFlames;
                    break;
                case BeatFlames when beatTick >= FlameRoundTime * FlameRounds:
                    TwinsStateContext.PincerBeat = BeatEjectCharge;
                    break;
                case BeatEjectCharge when beatTick >= EjectChargeTime:
                    TwinsStateContext.PincerBeat = BeatEject;
                    break;
                case BeatEject when beatTick >= EjectTime:
                    //弹射完成，释放抓取记录，进入恢复
                    TwinsStateContext.PincerGrabbedPlayer = -1;
                    TwinsStateContext.PincerBeat = BeatRecover;
                    break;
            }
        }

        #endregion

        #region 收尾与工具

        /// <summary>权威端把共享记录镜像进本眼 override ai 同步槽</summary>
        private void MirrorSyncSlots(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            float a8 = TwinsStateContext.PincerGrabbedPlayer >= 0
                ? TwinsStateContext.PincerGrabbedPlayer + 1 : 0f;
            float a9 = TwinsStateContext.PincerBeat;
            float a10 = TwinsStateContext.PincerLineAngle;
            if (Context.Ai[8] != a8 || Context.Ai[9] != a9) {
                npc.netUpdate = true;
            }
            Context.Ai[8] = a8;
            Context.Ai[9] = a9;
            Context.Ai[10] = a10;
        }

        /// <summary>终结一次投技：清共享记录并盖冷却戳，幂等</summary>
        private static void EndPincer(bool stampCooldown, bool wasWhiff) {
            TwinsStateContext.ResetPincerData();
            if (stampCooldown) {
                TwinsStateContext.PincerLastEndUpdate = Main.GameUpdateCount;
                TwinsStateContext.PincerLastWasWhiff = wasWhiff;
            }
        }

        /// <summary>运镜接管期间普通震屏可能失效，被抓者本地改走导演震动</summary>
        private static void GrabShake(Vector2 pos, float strength, int frames) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            if (CutsceneDirector.CurrentClip is TwinsPincerCutscene) {
                CutsceneDirector.Shake(Vector2.Zero, strength, 0.9f, frames);
                return;
            }
            TwinsMotion.Shake(pos, strength, frames);
        }

        /// <summary>投技仅二阶段触发，退回各自二阶段锚点</summary>
        private ITwinsState GetExitState() {
            if (Context.IsSpazmatism) {
                return new SpazmatismFlameChaseState(comboStep);
            }
            return new RetinazerVerticalBarrageState(comboStep);
        }

        public override void OnExit(TwinsStateContext context) {
            base.OnExit(context);
            DisableContactDamage(context.Npc);
            //异常切走(死亡演出等)也要放人、清同步槽
            if (!VaultUtils.isClient) {
                if (TwinsStateContext.PincerGrabbedPlayer >= 0 || TwinsStateContext.PincerBeat != BeatNone) {
                    EndPincer(stampCooldown: true, wasWhiff: false);
                }
                context.Ai[8] = 0f;
                context.Ai[9] = 0f;
                context.Ai[10] = 0f;
                context.Npc.netUpdate = true;
            }
            TwinsStateContext.ClearComboSignal();
        }

        #endregion
    }
}
