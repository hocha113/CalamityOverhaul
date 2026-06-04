using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    internal static class PrimeArmActions
    {
        public static void ChangeState(
            VaultStateMachine<PrimeArmStateContext> machine,
            IVaultState<PrimeArmStateContext> state,
            NPC npc,
            bool sync = true
        ) {
            machine?.ChangeState(state);
            if (sync) {
                npc.netUpdate = true;
            }
        }

        public static void ResetSharedTimer(NPC npc) {
            npc.ai[PrimeAiSlots.ArmSharedTimer] = 0f;
        }

        public static void ResetLocalCooldown(NPC npc) {
            npc.localAI[0] = 0f;
        }

        public static void TargetAndSync(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }

            npc.TargetClosest();
            npc.netUpdate = true;
        }

        public static Vector2 GetIdleAnchor(NPC head, NPC arm, float horizontalOffset, float verticalOffset) {
            return head.Center + new Vector2(horizontalOffset * arm.ai[PrimeAiSlots.ArmSide], verticalOffset);
        }

        public static bool IsHeadDespawnState(NPC head) {
            return head.ai[PrimeAiSlots.HeadAttackState] == 3f;
        }

        public static bool IsHeadDeathPerformance(NPC head) {
            return PrimeFacts.IsDeathPerformance(head);
        }
    }
}
