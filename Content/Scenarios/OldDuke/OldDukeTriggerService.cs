using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.OtherMods.BossChecklist;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke
{
    /// <summary>营地寻我剧情触发信道，客户端上行请求，服务端转播其余端</summary>
    internal class OldDukeTriggerService : CWRNetChannel
    {
        public override void Receive(BinaryReader reader, int whoAmI) => HandleStartCampsiteFindMeScenario(reader, whoAmI);

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
                ModPacket packet = CWRNetWork.GetPacket<OldDukeTriggerService>();
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
