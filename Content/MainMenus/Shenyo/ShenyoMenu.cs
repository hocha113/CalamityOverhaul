using Microsoft.Xna.Framework.Graphics;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.MainMenus.Shenyo
{
    /// <summary>鬼湖夜雨主题的 ModMenu 壳：主题登记与切换持久化交给 tML；
    /// 标题帧的整帧绘制由 <see cref="ShenyoMenuOverride"/> 接管</summary>
    internal class ShenyoMenu : ModMenu, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static LocalizedText ThemeName { get; private set; }

        public override string DisplayName => ThemeName?.Value ?? "Ghost Lake Rains";

        //主菜单 BGM：Assets/Sounds/Music/Rains.ogg（鬼雨主题曲，由 Main 每帧读 CurrentMenu.Music）
        public override int Music => MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/Rains");

        public override void SetStaticDefaults() {
            ThemeName = this.GetLocalization(nameof(ThemeName), () => "鬼湖夜雨");
        }

        public override void OnSelected() => ShenyoMenuOverride.OnThemeSelected();

        public override void OnDeselected() => ShenyoMenuOverride.OnThemeDeselected();

        //标题帧已在 DrawMenu 入口画过全景；子页面同样由 DrawMenu 入口铺氛围。
        //此处只隐藏 tML logo，避免与入口全景双重绘制
        public override bool PreDrawLogo(SpriteBatch spriteBatch, ref Vector2 logoDrawCenter,
            ref float logoRotation, ref float logoScale, ref Color drawColor) {
            return false;
        }
    }
}
