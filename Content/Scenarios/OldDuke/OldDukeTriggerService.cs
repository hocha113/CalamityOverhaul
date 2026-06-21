using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Scenarios.OldDuke.Campsites;
using CalamityOverhaul.OtherMods.BossChecklist;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke
{
    internal static class OldDukeTriggerService
    {
        public static void HandleStartCampsiteFindMeScenario(BinaryReader reader, int whoAmI) {
            int npcIndex = reader.ReadInt32();
            if (!npcIndex.TryGetNPC(out NPC npc)) {
                return;
            }

            if (BCKRef.Has) {
                BCKRef.SetActiveNPCEntryFlags(npc.whoAmI, -1);
            }

            npc.active = false;
            npc.netUpdate = true;

            if (VaultUtils.isServer) {
                ModPacket packet = CWRMod.Instance.GetPacket();
                packet.Write((byte)CWRMessageType.StartCampsiteFindMeScenario);
                packet.Write(npc.whoAmI);
                packet.Send(-1, whoAmI);
            }
            else {
                TriggerCampsiteScenario();
            }
        }

        public static bool TriggerCampsiteScenario() {
            if (NarrativeTriggerGate.IsBusy) {
                return false;
            }

            if (CWRRef.GetAcidRainEventIsOngoing()) {
                CampsiteInteractionDialogue.EntryMode = CampsiteInteractionDialogue.InteractionEntryMode.SparOnly;
                return NarrativeRouter.Begin<CampsiteInteractionDialogue>();
            }

            return NarrativeRouter.Begin<ComeCampsiteFindMe>();
        }
    }
}
