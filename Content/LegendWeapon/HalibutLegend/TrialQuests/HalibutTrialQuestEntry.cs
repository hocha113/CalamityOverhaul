using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using Terraria.Localization;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.TrialQuests
{
    /// <summary>
    /// 比目鱼传奇武器的单条试炼委托条目——<see cref="EntrustEntryData"/> 子类，
    /// 动态追踪目标Boss的存活状态与血量，为追踪窗口提供战斗进度显示：<br/>
    /// · Boss不在场时提示等待召唤<br/>
    /// · Boss存在时显示实时血量百分比
    /// </summary>
    internal class HalibutTrialQuestEntry : LegendTrialQuestEntry
    {
        public HalibutTrialQuestEntry(string key, LocalizedText title, LocalizedText summary, LocalizedText category)
            : base(key, title, summary, category) { }
    }
}
