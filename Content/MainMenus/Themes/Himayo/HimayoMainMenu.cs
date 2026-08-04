using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.MainMenus.Themes.Himayo
{
    [Autoload(Side = ModSide.Client)]
    internal sealed class HimayoMainMenu : ModMenu
    {
        public override string DisplayName => "Himayo";

        public override bool IsAvailable => true;

        public override int Music => MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/Future");

        public override void Load() => HimayoMenuTheme.LoadAssets(Mod);

        public override void Unload() => HimayoMenuTheme.UnloadAssets();

        public override void OnSelected() => HimayoMenuTheme.OnSelected();

        public override void OnDeselected() => HimayoMenuTheme.OnDeselected();

        public override bool PreDrawLogo(SpriteBatch spriteBatch, ref Vector2 logoDrawCenter,
            ref float logoRotation, ref float logoScale, ref Color drawColor) {
            HimayoMenuTheme.DrawBackground(spriteBatch);
            return HimayoMenuTheme.ShouldDrawNativeLogo;
        }

        public override void PostDrawLogo(SpriteBatch spriteBatch, Vector2 logoDrawCenter,
            float logoRotation, float logoScale, Color drawColor) {
            if (HimayoMenuVanillaBridge.BridgeOperational) {
                HimayoMenuTheme.DrawForeground(spriteBatch);
            }
        }
    }
}
