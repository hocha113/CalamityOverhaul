using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States
{
    /// <summary>侧位悬停：连接拍/呼吸口，末端选招；阶段转换在此裁决</summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.Hover, typeof(FishronStateContext))]
    internal class FishronHoverState : FishronStateBase
    {
        public override string StateName => "Hover";
        public override FishronStateIndex StateIndex => FishronStateIndex.Hover;

        private int hoverSide;

        public FishronHoverState() {
        }

        private static int HoverDuration(FishronStateContext ctx) {
            //三阶段的呼吸口再压三成：白眼形态几乎不给喘息
            int t = ctx.Phase == 3 ? 21 : ctx.Phase == 2 ? 38 : 46;
            if (ctx.IsAsuraMode) {
                t -= 6;
            }
            if (ctx.IsLandEnraged) {
                t -= 8;
            }
            return Math.Max(t, 18);
        }

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            hoverSide = 0;
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            if (hoverSide == 0) {
                hoverSide = Math.Sign(npc.Center.X - player.Center.X);
                if (hoverSide == 0) {
                    hoverSide = 1;
                }
            }

            //原版式侧位：±340 横向、-180 纵向
            Vector2 hoverGoal = player.Center + new Vector2(hoverSide * 340f, -180f);
            float speed = context.Phase == 3 ? 13f : context.Phase == 2 ? 11f : 9f;
            float accel = context.Phase == 3 ? 0.72f : 0.58f;
            if (context.IsLandEnraged) {
                speed += 5f;
                accel += 0.3f;
            }
            SetMovement(context, hoverGoal, speed, accel);

            Timer++;

            int duration = HoverDuration(context);
            bool positioned = Timer > duration * 0.5f && npc.WithinRange(hoverGoal, 190f);

            if (Timer > duration || positioned) {
                //只服务端/单人裁决
                if (!VaultUtils.isClient) {
                    return DecideNext(context);
                }
            }

            return null;
        }

        /// <summary>
        /// 裁决顺序：转阶段演出 > 大招 > 出招环。
        /// 被爆发伤害一口气打穿多个阈值时，也按 二转→三转→大招 逐次补拍，压迫感不跳拍
        /// </summary>
        private static IFishronState DecideNext(FishronStateContext context) {
            float ratio = context.LifeRatio;

            //阶段转换演出（旗标只在演出 OnEnter 落下）
            if (!context.PhaseTwoStarted && ratio < 0.65f) {
                return new FishronPhaseTwoTransitionState();
            }
            if (context.PhaseTwoStarted && !context.PhaseThreeStarted && ratio < 0.32f) {
                return new FishronPhaseThreeTransitionState();
            }
            //低血一次性大招
            if (!context.MaelstromUsed && context.PhaseThreeStarted && ratio < 0.14f) {
                return new FishronMaelstromState();
            }
            //投技：大漩涡卷客（二阶段解锁；冷却/时停/演出互斥门在 CanTrigger 内）
            if (FishronVortexSnareState.CanTrigger(context)) {
                return new FishronVortexSnareState();
            }

            return NextRingAttack(context);
        }

        /// <summary>手排出招环：压迫与呼吸交替，强招押后</summary>
        private static IFishronState NextRingAttack(FishronStateContext context) {
            IFishronState[] ring;
            if (context.Phase == 3) {
                ring = [
                    new FishronStormChainDashState(),
                    new FishronLightningRainState(),
                    new FishronVeilHuntState(),
                    new FishronTidalDashPrepareState(),
                    new FishronTsunamiSweepState(),
                    new FishronDiveBreachState(),
                    new FishronSharkronStrafeState(),
                    new FishronBubbleMazeState(),
                    new FishronVeilHuntState(),
                    new FishronRingSpinState(),
                ];
            }
            else if (context.Phase == 2) {
                ring = [
                    new FishronTidalDashPrepareState(),
                    new FishronRingSpinState(),
                    new FishronTsunamiSweepState(),
                    new FishronDiveBreachState(),
                    new FishronBubbleMazeState(),
                    new FishronSharkronStrafeState(),
                    new FishronTidalDashPrepareState(),
                    new FishronTornadoSummonState(),
                ];
            }
            else {
                ring = [
                    new FishronTidalDashPrepareState(),
                    new FishronBubbleMazeState(),
                    new FishronDiveBreachState(),
                    new FishronTidalDashPrepareState(),
                    new FishronTornadoSummonState(),
                    new FishronSharkronStrafeState(),
                ];
            }

            IFishronState next = ring[context.AttackRingIndex % ring.Length];
            context.AttackRingIndex++;

            //潜浪跃袭吃水位门：脚下没有真海面就退化成潮汐冲刺，不硬演
            if (next is FishronDiveBreachState && !FishronDiveBreachState.WaterReachable(context)) {
                next = new FishronTidalDashPrepareState();
            }
            return next;
        }
    }
}
