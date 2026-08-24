using CalamityOverhaul.Content.LegendWeapon.KikasaLegend;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Scenarios.Shenyo;
using InnoVault.GameSystem;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds
{
    /// <summary>
    /// 鬼雨主题曲 <c>Rains</c>：深潜去见沈幽起播，贯穿初遇、送出，直到鬼伞五步教程结束。<br/>
    /// 不走 <see cref="ModSceneEffect"/>（Event 压不过群系/事件）。在
    /// <see cref="IUpdateAudio.DecideMusic"/> 里写 <see cref="Main.musicBox2"/>，
    /// 赶在 <c>UpdateAudio</c> 消费 Music2 之前盖过 SceneEffect 的定曲。
    /// </summary>
    internal static class OniRainTheme
    {
        internal const string MusicPath = "CalamityOverhaul/Assets/Sounds/Music/Rains";

        /// <summary>本会话已武装：深潜/深层等到过沈幽窗口后，重进存档也接着播到教程结束</summary>
        private static bool sessionArmed;

        internal static bool ShouldPlay() {
            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return false;
            }
            if (player.GetModPlayer<StoryPlayer>().Get<KikasaGuideData>().GuideSeen) {
                sessionArmed = false;
                return false;
            }

            if (!ShenyoStorySync.PostFirstMetIsComplete) {
                bool approach = OniRainDescentTransition.Active
                    || OniRainWorldState.LocalDepth >= 2
                    || NarrativeRouter.IsActive<FirstMetShenyo>();
                if (approach) {
                    sessionArmed = true;
                }
                return approach;
            }

            //初遇已落幕、教程未看完：送出间隙、仍在雨里、发伞后、教程全程都接着播
            if (sessionArmed || OniRainExitTransition.Active || ShenyoStorySync.KikasaGranted
                || OniRainWorldState.LocalIn
                || player.HasItem(ModContent.ItemType<KikasaItem>())) {
                sessionArmed = true;
                return true;
            }
            return false;
        }

        internal static void Apply() {
            if (Main.gameMenu || Main.dedServ) {
                return;
            }
            if (!ShouldPlay()) {
                return;
            }
            if (CWRRef.GetBossRushActive() || VaultUtils.isServer) {
                return;
            }
            Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot(MusicPath);
        }

        internal static void Reset() => sessionArmed = false;
    }

    /// <summary>加载期单实例：挂在 DecideOnNewMusic 之后，专写 Music2</summary>
    internal sealed class OniRainThemeAudio : IUpdateAudio
    {
        void IUpdateAudio.DecideMusic() => OniRainTheme.Apply();
    }
}
