using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    public class CWRUI : ModSystem
    {
        public static bool DontSetHoverItem;
        public static Item HoverItem = new Item();
        public override void UpdateUI(GameTime gameTime) {
            base.UpdateUI(gameTime);
        }
        public override void PostUpdateEverything() {
            if (!DontSetHoverItem) {
                HoverItem = Main.HoverItem;
            }
            DontSetHoverItem = false;
        }
    }
}
