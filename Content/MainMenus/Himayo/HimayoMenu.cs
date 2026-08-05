using Microsoft.Xna.Framework;
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

        public override string DisplayName => ThemeName?.Value ?? "Sakura Night";

        //主菜单 BGM：Assets/Sounds/Music/Future.ogg（由 Main 每帧读 CurrentMenu.Music，不依赖 DrawMenu）
        public override int Music => MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/Future");

        public override void SetStaticDefaults() {
            ThemeName = this.GetLocalization(nameof(ThemeName), () => "夜樱境");
        }

        public override void OnSelected() => HimayoMenuOverride.OnThemeSelected();

        public override void OnDeselected() => HimayoMenuOverride.OnThemeDeselected();

        //标题帧被整帧接管时本钩子不会执行；其余 menuMode（角色选择/设置/多人等）原版路径行至 logo 段，
        //在此垫全景底图并隐藏 tML logo，保证子菜单与标题页视觉连续
        public override bool PreDrawLogo(SpriteBatch spriteBatch, ref Vector2 logoDrawCenter,
            ref float logoRotation, ref float logoScale, ref Color drawColor) {
            HimayoMenuOverride.DrawPanoramaBackdrop(spriteBatch);
            return false;
        }
    }
}
