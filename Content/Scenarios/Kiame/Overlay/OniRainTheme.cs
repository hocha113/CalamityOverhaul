using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Scenarios.Shenyo;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Overlay
{
    /// <summary>
    /// 鬼雨主题曲 <c>Rains</c>：播放窗口是每帧现算的叙事在场证明，没有随存档或会话的永真钥匙。<br/>
    /// 初遇前跟下潜/深层/初遇演出；初遇后只剩三个活口：送出演出中、身在鬼雨世界、
    /// 教程卡正在讲（<see cref="KikasaHudLead.CardVisible"/>）。教程走完（GuideSeen）永久退场；
    /// 「收起」（Declined）、失伞、被更高优先级引导压制时卡片不在场，主题随卡一起停，
    /// 湖心景「?」重讲时随卡回归。boss 在场让位战斗曲，打完教程还在讲就接着播。<br/>
    /// 不走 <see cref="ModSceneEffect"/>（Event 压不过群系/事件），经
    /// <see cref="MusicDirector"/> 传奇主题档认领覆盖（#97 的"主界面 BGM 在游戏内仍播放"
    /// 即此窗口失控所致，认领化后菜单档与游戏档由仲裁器硬隔离，赢家切换有日志可查）
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

            //三个活口无一为真即停：送出间隙、仍在雨里、教程卡在讲
            //（初遇后给 Boss 曲让位由认领的 YieldToBossMusic 表达，仲裁器统一判）
            return OniRainExitTransition.Active
                || OniRainWorldState.LocalIn
                || KikasaHudLead.CardVisible;
        }
    }

    /// <summary>鬼伞主题认领：初遇前演出压过 Boss 曲（沿旧语义），初遇后才让位；BossRush 恒让</summary>
    internal sealed class OniRainThemeClaim : MusicClaim
    {
        public override MusicTier Tier => MusicTier.LegendTheme;
        public override bool YieldToBossMusic => ShenyoStorySync.PostFirstMetIsComplete;
        public override bool YieldToBossRush => true;
        public override bool ShouldPlay() => OniRainTheme.ShouldPlay();
        public override int GetMusicSlot() => MusicLoader.GetMusicSlot(OniRainTheme.MusicPath);
    }
}
