using Microsoft.Xna.Framework.Graphics;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.MainMenus.Himayo
{
    /// <summary>夜樱主题的 ModMenu 壳：主题登记与切换持久化交给 tML；标题帧的整帧绘制由 <see cref="HimayoMenuOverride"/> 接管</summary>
    internal class HimayoMenu : ModMenu, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static LocalizedText ThemeName { get; private set; }

        //曲名锁图（平假名无法走 MouseText）
        [VaultLoaden(CWRConstant.Asset + "MainMenus/Himayo/")]
        public static Texture2D AsuENoKakehashi = null;

        public override string DisplayName => ThemeName?.Value ?? "Sakura Night";

        //主菜单 BGM：Assets/Sounds/Music/Future.ogg（由 Main 每帧读 CurrentMenu.Music，不依赖 DrawMenu）
        public override int Music => MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/Future");

        public override void SetStaticDefaults() {
            ThemeName = this.GetLocalization(nameof(ThemeName), () => "夜樱境");
        }

        public override void OnSelected() => HimayoMenuOverride.OnThemeSelected();

        public override void OnDeselected() => HimayoMenuOverride.OnThemeDeselected();

        //标题帧已在 DrawMenu 入口画过全景；子页面同样由 DrawMenu 入口铺氛围。
        //此处只隐藏 tML logo，避免与入口全景双重绘制
        public override bool PreDrawLogo(SpriteBatch spriteBatch, ref Vector2 logoDrawCenter,
            ref float logoRotation, ref float logoScale, ref Color drawColor) {
            return false;
        }
    }
}
