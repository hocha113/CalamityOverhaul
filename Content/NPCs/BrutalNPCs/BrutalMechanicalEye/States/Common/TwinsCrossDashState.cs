using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common
{
    /// <summary>交叉冲刺合击：水平/垂直两侧十字冲，交点冲击环</summary>
    /// <para>一阶段轻量合击；二阶段大招未解锁时替补</para>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.TwinsCrossDash, typeof(TwinsStateContext))]
    internal class TwinsCrossDashState : TwinsStateBase
    {
        public override string StateName => "TwinsCrossDash";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.TwinsCrossDash;

        private int GatherPhase => Context.IsDeathMode ? 44 : 52;
        private int ChargePhase => Context.IsDeathMode ? 38 : 46;
        private const int DashPhase = 26;
        private const int WhipPhase = 14;
        private const int RecoveryPhase = 22;
        private const int MaxPartnerWait = 120;

        private int TotalDuration => GatherPhase + ChargePhase + DashPhase + WhipPhase + RecoveryPhase;
        private float DashSpeed => Context.IsDeathMode ? 34f : 30f;

        private TwinsStateContext Context;
        private int comboStep;
        private int partnerWait;
        private Vector2 lockedDirection;
        private Vector2 crossPoint;
        private bool hasLaunched;
        private bool hasCrossRippled;

        public TwinsCrossDashState() : this(0) {
        }

        public TwinsCrossDashState(int currentComboStep) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            partnerWait = 0;
            hasLaunched = false;
            hasCrossRippled = false;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //搭档失效→直接退出合击
            NPC partner = TwinsStateContext.GetPartnerNpc(npc.type);
            if (!partner.Alives()) {
                TwinsStateContext.ClearComboSignal();
                return GetExitState();
            }

            Timer++;

            if (Timer <= GatherPhase) {
                ExecuteGatherPhase(npc, player);

                //集合末尾标记就绪，等待双方都集合完成再同拍推进
                if (Timer == GatherPhase) {
                    TwinsStateContext.MarkComboReady(context.IsSpazmatism);
                    if (!TwinsStateContext.BothComboReady && partnerWait < MaxPartnerWait) {
                        Timer--;
                        partnerWait++;
                    }
                }
            }
            else if (Timer <= GatherPhase + ChargePhase) {
                ExecuteChargePhase(npc, player);
            }
            else if (Timer <= GatherPhase + ChargePhase + DashPhase) {
                ExecuteDashPhase(npc, player);
            }
            else if (Timer <= GatherPhase + ChargePhase + DashPhase + WhipPhase) {
                //急停甩头
                DisableContactDamage(npc);
                TwinsMotion.BrakeAndWhip(npc, player.Center, 0.76f, 0.32f);
                context.PushDashVisuals(0.4f, 0.7f);
            }
            else {
                //恢复
                npc.velocity *= 0.92f;
                FaceTarget(npc, player.Center);
            }

            if (Timer >= TotalDuration) {
                return GetExitState();
            }

            return null;
        }

        /// <summary>
        /// 集合阶段：魔焰眼占水平侧，激光眼占垂直侧，形成十字夹角
        /// </summary>
        private void ExecuteGatherPhase(NPC npc, Player player) {
            float progress = Timer / (float)GatherPhase;

            Vector2 targetPos = Context.IsSpazmatism
                ? player.Center + new Vector2(npc.Center.X < player.Center.X ? -520 : 520, 0)
                : player.Center + new Vector2(0, npc.Center.Y < player.Center.Y ? -520 : 520);
            TwinsMotion.SpringHover(npc, targetPos, 0.02f, 0.1f);
            FaceTarget(npc, player.Center);

            Context.SetChargeState(1, progress * 0.4f);

            if (Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.2f, Volume = 0.8f }, npc.Center);
            }
        }

        /// <summary>
        /// 蓄力阶段：锁定穿过玩家的冲刺线，末段绷紧颤抖
        /// </summary>
        private void ExecuteChargePhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase;
            float progress = phaseTimer / (float)ChargePhase;

            npc.velocity *= 0.85f;
            if (progress > 0.75f && !VaultUtils.isServer) {
                npc.position += Main.rand.NextVector2Circular(1.8f, 1.8f);
            }

            //持续修正瞄准，末1/4锁定
            if (progress <= 0.75f) {
                crossPoint = player.Center;
                lockedDirection = (crossPoint - npc.Center).SafeNormalize(Vector2.UnitY);
            }
            npc.rotation = lockedDirection.ToRotation() - MathHelper.PiOver2;

            Context.SetChargeState(1, 0.4f + progress * 0.6f);

            //能量内聚
            if (phaseTimer % 2 == 0) {
                TwinsMotion.ChargeGatherFX(npc.Center, Context.IsSpazmatism, progress, 85f);
            }

            //蓄力完成闪光
            if (phaseTimer == ChargePhase - 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.25f }, npc.Center);
            }
        }

        /// <summary>
        /// 冲刺阶段：双眼同步十字穿越，交点爆出涟漪
        /// </summary>
        private void ExecuteDashPhase(NPC npc, Player player) {
            Context.ResetChargeState();

            if (!hasLaunched) {
                hasLaunched = true;
                TwinsMotion.DashLaunch(npc, lockedDirection, DashSpeed, Context.IsSpazmatism, 1.15f);
                //十字合击同步释放：天空闪雷劈向交点（双眼同帧触发时自动只保留一次）
                MachineEffect.TriggerSkyFlash(crossPoint, 0.75f);
            }

            //每帧启用碰撞伤害(控制器每帧会重置激光眼的伤害)
            EnableContactDamage(npc);
            FaceVelocity(npc);
            Context.PushDashVisuals(1f, 1f);

            //冲刺拖尾
            if (!VaultUtils.isServer && Timer % 2 == 0) {
                PRTLoader.NewParticle<PRT_TwinsSpark>(
                    npc.Center - npc.velocity.SafeNormalize(Vector2.Zero) * 30f + Main.rand.NextVector2Circular(12, 12),
                    -npc.velocity * 0.15f, Color.White, Main.rand.NextFloat(1f, 1.6f))?
                    .Configure(15, Context.IsSpazmatism ? 1 : 0);
            }

            //经过交点瞬间留下涟漪冲击环(只触发一次)
            if (!hasCrossRippled && Vector2.Distance(npc.Center, crossPoint) < 70f) {
                hasCrossRippled = true;
                if (!VaultUtils.isServer) {
                    Color themeColor = Context.IsSpazmatism ? TwinsMotion.SpazColor : TwinsMotion.RetinColor;
                    PRTLoader.NewParticle<PRT_DWave>(crossPoint, Vector2.Zero, themeColor, 0.2f)?
                        .Configure(Vector2.One, 0f, 1.5f, 20);
                    PRTLoader.NewParticle<PRT_DWave>(crossPoint, Vector2.Zero, Color.White * 0.8f, 0.12f)?
                        .Configure(Vector2.One, 0f, 0.9f, 14);
                    for (int i = 0; i < 10; i++) {
                        PRTLoader.NewParticle<PRT_TwinsSpark>(crossPoint, VaultUtils.RandVr(4, 11),
                            Color.White, Main.rand.NextFloat(1.1f, 1.8f))?.Configure(18, Context.IsSpazmatism ? 1 : 0);
                    }
                    TwinsMotion.Shake(crossPoint, 5f, 10);
                }
            }
        }

        /// <summary>
        /// 退出状态：按所处阶段返回各自锚点
        /// </summary>
        private ITwinsState GetExitState() {
            if (Context.IsSpazmatism) {
                return Context.IsSecondPhase
                    ? new SpazmatismFlameChaseState(comboStep)
                    : new SpazmatismHoverShootState(comboStep);
            }
            return Context.IsSecondPhase
                ? new RetinazerVerticalBarrageState(comboStep)
                : new RetinazerHoverShootState(comboStep);
        }

        public override void OnExit(TwinsStateContext context) {
            base.OnExit(context);
            DisableContactDamage(context.Npc);
            TwinsStateContext.ClearComboSignal();
        }
    }
}
