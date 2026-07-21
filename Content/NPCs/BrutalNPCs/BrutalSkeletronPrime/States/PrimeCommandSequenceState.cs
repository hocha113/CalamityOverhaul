using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Projectiles;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>武装指挥hub，7步序列</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.CommandSequence, typeof(PrimeStateContext))]
    internal class PrimeCommandSequenceState : PrimeStateBase
    {
        public override string StateName => "CommandSequence";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.CommandSequence;

        internal static int TelegraphLead => 30;

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 0;

            Movement(context);
            LeanByVelocity(npc);

            int hubDuration = PrimeDirector.CommandHubFrames;
            if (context.MasterMode) {
                hubDuration -= 15;
            }
            if (context.BossRush) {
                hubDuration -= 20;
            }

            int remaining = hubDuration - Timer;

            //出招门闸，等蓄力兑现
            if (remaining == TelegraphLead && NextStepHijacksArms(context) && PrimeFacts.AnyArmCommitted()) {
                return null;
            }

            if (remaining <= TelegraphLead) {
                context.SetChargeState(1, 1f - remaining / (float)TelegraphLead);
                if (!VaultUtils.isClient && remaining == TelegraphLead) {
                    BroadcastTelegraph(context);
                }
            }

            Timer++;
            if (Timer >= hubDuration && !VaultUtils.isClient) {
                npc.TargetClosest();
                npc.netUpdate = true;
                return DispatchNext(context);
            }
            return null;
        }

        /// <summary>下一步是否接管四臂</summary>
        private static bool NextStepHijacksArms(PrimeStateContext context) {
            int step = context.AttackPhaseIndex % 7;
            return step is 1 or 3 or 4 or 5 or 6;
        }

        private static void BroadcastTelegraph(PrimeStateContext context) {
            int step = context.AttackPhaseIndex % 7;

            //冲撞步方向线预警
            if (step is 1 or 5) {
                PrimeTelegraphLine.SpawnLine(context.Npc, context.Npc.Center,
                    DirectionToTarget(context).ToRotation(), TelegraphLead);
            }

            PrimeCommandKind cmd = step switch {
                0 => PrimeCommandKind.PhysicalAssault,
                2 => PrimeCommandKind.FireSuppression,
                4 => PrimeCommandKind.CrossExecute,
                _ => PrimeCommandKind.None,
            };
            context.Npc.ai[PrimeAiSlots.HeadCommandSlot] = (float)cmd;
        }

        private static IPrimeState DispatchNext(PrimeStateContext context) {
            int step = context.AttackPhaseIndex % 7;
            context.AttackPhaseIndex++;

            return step switch {
                0 => BeginCommand(context, PrimeCommandKind.PhysicalAssault),
                1 => new PrimeSpinDashState(),
                2 => BeginCommand(context, PrimeCommandKind.FireSuppression),
                3 => new PrimeBarrageCommandState(),
                4 => new PrimeCrossExecuteState(),
                5 => new PrimeSpinDashState(),
                _ => new PrimeTetherSpinState(),
            };
        }

        private static IPrimeState BeginCommand(PrimeStateContext context, PrimeCommandKind kind) {
            context.Npc.ai[PrimeAiSlots.HeadCommandSlot] = (float)kind;
            return new PrimeCommandExecuteState(kind);
        }

        private void Movement(PrimeStateContext context) {
            float vAccel = Main.masterMode ? 0.04f : 0.03f;
            float vMax = Main.masterMode ? 5f : 4f;
            float hAccel = Main.masterMode ? 0.1f : 0.08f;
            float hMax = Main.masterMode ? 10f : 9f;
            float decel = Main.masterMode ? 0.94f : 0.96f;
            HoverMovement(context, vAccel, vMax, hAccel, hMax, decel, 200, 480);
        }
    }

}

