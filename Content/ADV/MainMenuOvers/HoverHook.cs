using InnoVault.GameSystem;
using Terraria;

namespace CalamityOverhaul.Content.ADV.MainMenuOvers
{
    internal class HoverHook : MenuOverride
    {
        public override void PostDrawModMenu(GameTime gameTime, Color color, float logoRotation, float logoScale) {
            SupCalPortraitUI.Instance.Update();
            HelenPortraitUI.Instance.Update();
            SupCalPortraitUI.Instance.Draw(Main.spriteBatch);
            HelenPortraitUI.Instance.Draw(Main.spriteBatch);
        }
    }
}
