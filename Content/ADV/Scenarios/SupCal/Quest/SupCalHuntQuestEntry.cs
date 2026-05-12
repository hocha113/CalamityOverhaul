using CalamityOverhaul.Content.ADV.EntrustManager;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using static CalamityOverhaul.Content.ADV.Common.BaseDamageTracker;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.Quest
{
    /// <summary>
    /// 硫火女巫猎杀委托条目——<see cref="EntrustEntryData"/> 子类，
    /// 动态追踪Boss存活状态与伤害贡献度，
    /// 为追踪窗口提供上下文感知的内容显示：<br/>
    /// · Boss不存在时提示玩家召唤目标<br/>
    /// · Boss存在时显示实时伤害贡献与进度
    /// </summary>
    internal class SupCalHuntQuestEntry : EntrustEntryData
    {
        /// <summary>目标Boss的NPC type</summary>
        public int TargetNpcType { get; init; }

        /// <summary>所需的伤害贡献度阈值（0.8 = 80%）</summary>
        public float RequiredContribution { get; init; }

        /// <summary>Boss不在场时的提示文本，{0} 为Boss名称</summary>
        public LocalizedText SummonHintFormat { get; init; }

        /// <summary>伤害贡献度的格式文本，{0} 为当前贡献度</summary>
        public LocalizedText ContributionFormat { get; init; }

        /// <summary>所需贡献度的格式文本，{0} 为所需阈值</summary>
        public LocalizedText RequiredFormat { get; init; }

        private bool isBossAlive;
        private float currentContribution;

        public SupCalHuntQuestEntry(string key, LocalizedText title, LocalizedText summary, LocalizedText category)
            : base(key, title, summary, category) { }

        //追踪窗口使用极简HUD样式时，需要在内容区顶部预留5px，
        //让标题下划线与首行描述之间留出可见的呼吸空间
        public override float GetTrackerContentTopPadding() => 5f;

        public override void OnUpdate() {
            if (Status == QuestEntryStatus.Completed || Status == QuestEntryStatus.Failed
                || Status == QuestEntryStatus.Suspended) return;

            isBossAlive = false;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == TargetNpcType) {
                    isBossAlive = true;
                    break;
                }
            }

            if (isBossAlive) {
                var tracker = CurrentDamageTrackerInstance;
                if (tracker?.NPC?.active == true
                    && tracker.TargetNPCType == TargetNpcType
                    && IsBossFightActive) {
                    var (weaponDmg, totalDmg, _) = GetDamageTrackingData();
                    currentContribution = totalDmg > 0 ? weaponDmg / totalDmg : 0f;
                    Progress = MathHelper.Clamp(currentContribution / RequiredContribution, 0f, 1f);
                }
                else {
                    currentContribution = 0f;
                    Progress = 0f;
                }
            }
            else {
                currentContribution = 0f;
                Progress = 0f;
            }
        }

        public override List<string> GetTrackerDetails() {
            if (!isBossAlive) {
                string bossName = Lang.GetNPCNameValue(TargetNpcType);
                return [string.Format(SummonHintFormat?.Value ?? "{0}", bossName)];
            }

            return [
                string.Format(ContributionFormat?.Value ?? "{0:0%}", currentContribution),
                string.Format(RequiredFormat?.Value ?? "{0:0%}", RequiredContribution)
            ];
        }
    }
}
