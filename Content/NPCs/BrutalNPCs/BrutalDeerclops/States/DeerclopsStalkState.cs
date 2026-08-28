using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States
{
    /// <summary>连接态：压迫式逼近+选招。是唯一放行暴风雪瞬步阀的状态</summary>
    [InnoVault.StateMachines.VaultState((int)DeerclopsStateIndex.Stalk, typeof(DeerclopsStateContext))]
    internal class DeerclopsStalkState : DeerclopsStateBase
    {
        public override string StateName => "Stalk";
        public override DeerclopsStateIndex StateIndex => DeerclopsStateIndex.Stalk;
        public override bool AllowBlizzardStep => true;

        private int BaseDuration(DeerclopsStateContext ctx) {
            int duration = ctx.IsPhase2 ? 45 : 58;
            if (ctx.IsAsuraMode) {
                duration -= 8;
            }
            return duration;
        }

        public override IDeerclopsState OnUpdate(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            context.MoveSpeedMult = 1.15f;
            npc.damage = npc.defDamage;

            int duration = BaseDuration(context);
            float dist = player != null ? npc.Distance(player.Center) : 9999f;

            //远则多走一会，近则提前出招，节奏服务于压迫
            bool timeUp = Timer > duration && dist < 860f;
            bool hardTimeUp = Timer > duration + 65;
            bool closeEarly = Timer > 22 && dist < 400f;

            if (timeUp || hardTimeUp || closeEarly) {
                if (!VaultUtils.isClient) {
                    return ChooseNextAttack(context);
                }
            }
            return null;
        }

        /// <summary>服务端/单机选招：大招与转阶段优先，其余走手排环</summary>
        private IDeerclopsState ChooseNextAttack(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            float lifeRatio = npc.life / (float)npc.lifeMax;

            if (!context.WhiteoutUsed && lifeRatio <= 0.28f) {
                return new DeerclopsWhiteoutState();
            }
            if (!context.IsPhase2 && lifeRatio <= 0.55f) {
                return new DeerclopsPhaseRoarState();
            }

            //手排出招环：压迫→机动→区域→心理交替
            IDeerclopsState[] normalCycle = [
                new DeerclopsSpikeWaveState(),
                new DeerclopsShadowClawState(),
                new DeerclopsFrostQuakeState(),
                new DeerclopsRubbleTossState(),
                new DeerclopsGazeRoarState(),
                new DeerclopsSpikeCageState(),
                new DeerclopsAvalancheChargeState(),
            ];

            IDeerclopsState[] phase2Cycle = [
                new DeerclopsAvalancheChargeState(),
                new DeerclopsSpikeWaveState(),
                new DeerclopsGazeRoarState(),
                new DeerclopsShadowClawState(),
                new DeerclopsSpikeCageState(),
                new DeerclopsSeizeHuntState(),
                new DeerclopsFrostQuakeState(),
                new DeerclopsRubbleTossState(),
                new DeerclopsGazeRoarState(),
                new DeerclopsAvalancheChargeState(),
            ];

            IDeerclopsState[] cycle = context.IsPhase2 ? phase2Cycle : normalCycle;
            IDeerclopsState next = cycle[context.AttackPhaseIndex % cycle.Length];
            context.AttackPhaseIndex++;
            //投技有额外门槛(冷却/距离/时停等)，未就绪时顶替为压迫招不空拍
            if (next is DeerclopsSeizeHuntState && !DeerclopsSeizeHuntState.GrabReady(context)) {
                next = new DeerclopsSpikeWaveState();
            }
            return next;
        }
    }
}
