using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using Terraria.Localization;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.TrialQuests
{
    /// <summary>
    /// 鬼妖村正的单条试炼委托条目——<see cref="EntrustEntryData"/> 子类，
    /// 动态追踪目标Boss的存活状态与血量，为追踪窗口提供战斗进度显示
    /// </summary>
    internal class MurasamaTrialQuestEntry : LegendTrialQuestEntry
    {
        public MurasamaTrialQuestEntry(string key, LocalizedText title, LocalizedText summary, LocalizedText category)
            : base(key, title, summary, category) { }
    }
}
