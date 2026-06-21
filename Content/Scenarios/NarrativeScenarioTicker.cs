using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers;
using CalamityOverhaul.Content.Scenarios.Draedon.Tzeentch;
using CalamityOverhaul.Content.Scenarios.Shepel;
using CalamityOverhaul.Content.Scenarios.Shepel.Gifts;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios
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
