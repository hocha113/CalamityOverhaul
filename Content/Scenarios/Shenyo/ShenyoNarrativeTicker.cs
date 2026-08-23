using CalamityOverhaul.Content.Scenarios.Shenyo.Gifts;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo
{
    internal sealed class ShenyoNarrativeTicker : ModSystem
    {
        public override void OnWorldLoad() {
            ShenyoGiftNarrativeTracker.ResetWorldState();
        }

        public override void PreUpdatePlayers() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }

            ShenyoGiftNarrativeTracker.Tick();
        }
    }
}
