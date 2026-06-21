using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen
{
    internal sealed class HalibutNarrativeTicker : ModSystem
    {
        public override void OnWorldLoad() {
            Quest.FishoilQuest.FishoilQuestScenario.ResetWorldState();
            HelensInterference.ResetWorldState();
            Gifts.HelenGiftNarrativeTracker.ResetWorldState();
        }

        public override void PreUpdatePlayers() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }

            Quest.FishoilQuest.FishoilQuestScenario.Tick();
            HelensInterference.Tick();
            Gifts.HelenGiftNarrativeTracker.Tick();
        }
    }
}
