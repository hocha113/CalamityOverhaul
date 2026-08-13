using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>衔接拍：优雅归位滑翔+攻击选择（手写循环表，强度交替）</summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.Connector, typeof(EmpressStateContext))]
    internal class EmpressConnectorState : EmpressStateBase
    {
        public override string StateName => "EmpressConnector";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.Connector;

        /// <summary>一阶段攻击循环：压制→机动→爆发→控场交替</summary>
        private static readonly EmpressStateIndex[] Phase1Cycle = [
            EmpressStateIndex.PrismRings,
            EmpressStateIndex.CrescentDash,
            EmpressStateIndex.SwordRain,
            EmpressStateIndex.LanceGrid,
            EmpressStateIndex.InterferenceWeave,
            EmpressStateIndex.CrescentDash,
            EmpressStateIndex.ConvergingCage,
            EmpressStateIndex.RadiantDance,
        ];

        /// <summary>二阶段攻击循环</summary>
        private static readonly EmpressStateIndex[] Phase2Cycle = [
            EmpressStateIndex.LanceGrid,
            EmpressStateIndex.PrismRings,
            EmpressStateIndex.CrescentDash,
            EmpressStateIndex.EverlastingBloom,
            EmpressStateIndex.SwordRain,
            EmpressStateIndex.InterferenceWeave,
            EmpressStateIndex.CrescentDash,
            EmpressStateIndex.ConvergingCage,
            EmpressStateIndex.RadiantDance,
            EmpressStateIndex.SwordRain,
        ];

        private int Duration => Context.Scaled(30);
        private EmpressStateContext Context;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            context.Pose = EmpressPose.Idle;
            context.PoseTimer = 0f;

            //归位滑翔：目标上方，带远距减速的原版DashTo形状
            if (target.Alives()) {
                Vector2 dest = target.Center + new Vector2(0f, -380f);
                if (npc.Distance(dest) > 200f) {
                    dest -= npc.DirectionTo(dest) * 100f;
                }
                Vector2 toDest = dest - npc.Center;
                float lerpValue = Utils.GetLerpValue(100f, 600f, toDest.Length(), clamped: true);
                float speed = System.Math.Min(toDest.Length(), context.IsDeathMode ? 24f : 21f);
                Vector2 desired = Vector2.Lerp(toDest.SafeNormalize(Vector2.Zero) * speed, toDest / 6f, lerpValue);
                npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.2f);
            }
            else {
                npc.velocity *= 0.92f;
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);

            if (Timer < Duration) {
                return null;
            }

            //客户端不选招：等ai[2]同步跟随，防计数器与侧滑冲量在本地空转
            if (VaultUtils.isClient) {
                return null;
            }

            //到点选择下一动作（权威端）
            return PickNext(context, npc, target);
        }

        /// <summary>攻击选择：特判优先，其余走循环表</summary>
        private IEmpressState PickNext(EmpressStateContext context, NPC npc, Player target) {
            //目标失效/过远→离场
            if (!target.Alives() || npc.Distance(target.Center) > 6400f) {
                return new EmpressDespawnState();
            }

            //真昼形态在黄昏/入夜时离去（原版规约）
            if (NPC.ShouldEmpressBeEnraged() && Main.dayTime && Main.time >= 53400.0) {
                return new EmpressDespawnState();
            }
            if (!Main.dayTime && ((int)npc.ai[3] & 2) != 0) {
                return new EmpressDespawnState();
            }

            //半血转阶段
            if (!context.IsSecondPhase && npc.life <= npc.lifeMax * 0.5f) {
                return new EmpressPhaseTransitionState();
            }

            //低血大招，一场一次
            float overdriveGate = context.IsDeathMode ? 0.3f : 0.25f;
            if (context.IsSecondPhase && !context.OverdriveUsed && npc.life <= npc.lifeMax * overdriveGate) {
                return new EmpressPrismOverdriveState();
            }

            //循环表取招
            EmpressStateIndex[] cycle = context.IsSecondPhase ? Phase2Cycle : Phase1Cycle;
            EmpressStateIndex pick = cycle[context.AttackCounter % cycle.Length];
            context.AttackCounter++;

            //起手侧滑：非静场攻击前给一记优雅的横向摆动（原版规约）
            if (pick != EmpressStateIndex.RadiantDance && pick != EmpressStateIndex.EverlastingBloom && target.Alives()) {
                int side = target.Center.X > npc.Center.X ? 1 : -1;
                npc.velocity = npc.DirectionFrom(target.Center).SafeNormalize(Vector2.Zero)
                    .RotatedBy(MathHelper.PiOver2 * side) * 19f;
            }

            return CreateState(pick);
        }

        internal static IEmpressState CreateState(EmpressStateIndex index) {
            return index switch {
                EmpressStateIndex.PrismRings => new EmpressPrismRingsState(),
                EmpressStateIndex.LanceGrid => new EmpressLanceGridState(),
                EmpressStateIndex.SwordRain => new EmpressSwordRainState(),
                EmpressStateIndex.RadiantDance => new EmpressRadiantDanceState(),
                EmpressStateIndex.ConvergingCage => new EmpressConvergingCageState(),
                EmpressStateIndex.InterferenceWeave => new EmpressInterferenceWeaveState(),
                EmpressStateIndex.CrescentDash => new EmpressCrescentDashState(),
                EmpressStateIndex.EverlastingBloom => new EmpressEverlastingBloomState(),
                EmpressStateIndex.PrismOverdrive => new EmpressPrismOverdriveState(),
                _ => new EmpressConnectorState(),
            };
        }
    }
}
