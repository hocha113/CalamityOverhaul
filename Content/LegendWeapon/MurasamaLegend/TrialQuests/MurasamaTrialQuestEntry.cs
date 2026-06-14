using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using Terraria.Localization;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.TrialQuests
{
    /// <summary>
    /// 鬼妖村正单条试炼委托，<see cref="EntrustEntryData"/>子类
    /// 追踪目标Boss存活与血量，供追踪窗口进度
    /// </summary>
    internal class MurasamaTrialQuestEntry : LegendTrialQuestEntry
    {
        public MurasamaTrialQuestEntry(string key, LocalizedText title, LocalizedText summary, LocalizedText category)
            : base(key, title, summary, category) { }
    }
}
