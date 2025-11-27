using InnoVault.GameSystem;
using Terraria;

namespace CalamityOverhaul.Content.ADV.MainMenuOvers
{
    internal class HoverHook : MenuOverride
    {
        public override void PostDrawModMenu(GameTime gameTime, Color color, float logoRotation, float logoScale) {
            if (SupCalPortraitUI.Instance.Active) {
                SupCalPortraitUI.Instance.Update();
                SupCalPortraitUI.Instance.Draw(Main.spriteBatch);
            }
            if (HelenPortraitUI.Instance.Active) {
                HelenPortraitUI.Instance.Update();
                HelenPortraitUI.Instance.Draw(Main.spriteBatch);
            }
        }
    }
}
