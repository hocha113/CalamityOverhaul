using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI;
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
    /// 鬼雨主题曲 <c>Rains</c>：播放窗口是每帧现算的叙事在场证明，没有随存档或会话的永真钥匙。<br/>
    /// 初遇前跟下潜/深层/初遇演出；初遇后只剩三个活口：送出演出中、身在鬼雨世界、
    /// 教程卡正在讲（<see cref="KikasaHudLead.CardVisible"/>）。教程走完（GuideSeen）永久退场；
    /// 「收起」（Declined）、失伞、被更高优先级引导压制时卡片不在场，主题随卡一起停，
    /// 湖心景「?」重讲时随卡回归。boss 在场让位战斗曲，打完教程还在讲就接着播。<br/>
    /// 不走 <see cref="ModSceneEffect"/>（Event 压不过群系/事件）。在
    /// <see cref="IUpdateAudio.DecideMusic"/> 里写 <see cref="Main.musicBox2"/>，
    /// 赶在 <c>UpdateAudio</c> 消费 Music2 之前盖过 SceneEffect 的定曲；
    /// 停写后原版 Player.Update 每帧复位 musicBox2，不会残留旧值。
    /// </summary>
    internal static class OniRainTheme
    {
        internal const string MusicPath = "CalamityOverhaul/Assets/Sounds/Music/Rains";

        internal static bool ShouldPlay() {
            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return false;
            }
            if (player.GetModPlayer<StoryPlayer>().Get<KikasaGuideData>().GuideSeen) {
                return false;
            }

            if (!ShenyoStorySync.PostFirstMetIsComplete) {
                //初遇前：只随接近与演出本身存亡，中途折返即停，无闩锁
                return OniRainDescentTransition.Active
                    || OniRainWorldState.LocalDepth >= 2
                    || NarrativeRouter.IsActive<FirstMetShenyo>();
            }

            //初遇后：boss 在场让位战斗曲
            if (Main.CurrentFrameFlags.AnyActiveBossNPC) {
                return false;
            }
            //三个活口无一为真即停：送出间隙、仍在雨里、教程卡在讲
            return OniRainExitTransition.Active
                || OniRainWorldState.LocalIn
                || KikasaHudLead.CardVisible;
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
    }

    /// <summary>加载期单实例：挂在 DecideOnNewMusic 之后，专写 Music2</summary>
    internal sealed class OniRainThemeAudio : IUpdateAudio
    {
        void IUpdateAudio.DecideMusic() => OniRainTheme.Apply();
    }
}
