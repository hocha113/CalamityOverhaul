using CalamityOverhaul.Content.MainMenus.Shenyo;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative
{
    /// <summary>夜樱 / 鬼湖主题曲提示的身份锁：菜单与局内初见共用，避免封面与风格再漂</summary>
    internal static class ThemeMusicToast
    {
        internal const int DisplayDuration = 360;

        internal static void ShowHimayo(float layoutScale = 1f, Func<float> screenYProvider = null) {
            if (Main.dedServ) {
                return;
            }
            MusicToast.ShowMusic(
                title: "凭夜:未来",
                albumCover: ADVAsset.Himayo_grin,
                style: MusicToast.MusicStyle.Sakura,
                displayDuration: DisplayDuration,
                screenYProvider: screenYProvider,
                layoutScale: layoutScale);
        }

        internal static void ShowShenyo(float layoutScale = 1f, Func<float> screenYProvider = null) {
            if (Main.dedServ) {
                return;
            }
            MusicToast.ShowMusic(
                title: "夢のきざはし",
                albumCover: ADVAsset.Shenyo_Calm,
                style: MusicToast.MusicStyle.WetInk,
                displayDuration: DisplayDuration,
                titleTexture: ShenyoMenu.YumeNoKizahashi,
                screenYProvider: screenYProvider,
                layoutScale: layoutScale);
        }
    }
}
