using CalamityOverhaul.Content.Narrative.Runtime;
using CalamityOverhaul.Content.Narrative.Scenarios.Draedon.Quest.DeploySignaltowers;
using CalamityOverhaul.Content.Narrative.Scenarios.Draedon.Tzeentch;
using CalamityOverhaul.Content.Narrative.Scenarios.Shepel;
using CalamityOverhaul.Content.Narrative.Scenarios.Shepel.Gifts;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios
{
    internal sealed class NarrativeScenarioTicker : ModSystem
    {
        public override void OnWorldLoad() {
            ShepelGiftNarrativeTracker.ResetWorldState();
            DeploySignaltowerScenario.ResetWorldState();
            FirstMetTzeentch.ResetWorldState();
            SHPCNarrativeRouter.RegisterAll();
        }

        public override void PreUpdatePlayers() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }

            ShepelGiftNarrativeTracker.Tick();
            DeploySignaltowerScenario.Tick();
            FirstMetTzeentch.Tick();
        }
    }
}
