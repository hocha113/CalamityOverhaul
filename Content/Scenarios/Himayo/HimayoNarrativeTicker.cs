using CalamityOverhaul.Content.Scenarios.Himayo.Gifts;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo
{
    internal sealed class HimayoNarrativeTicker : ModSystem
    {
        public override void OnWorldLoad() {
            HimayoGiftNarrativeTracker.ResetWorldState();
        }

        public override void PreUpdatePlayers() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }

            HimayoGiftNarrativeTracker.Tick();
        }
    }
}
