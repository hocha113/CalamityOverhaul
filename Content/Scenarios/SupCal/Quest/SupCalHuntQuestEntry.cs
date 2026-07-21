using CalamityOverhaul.Content.EntrustManager;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using static CalamityOverhaul.Content.Narrative.Common.BaseDamageTracker;

namespace CalamityOverhaul.Content.Scenarios.SupCal.Quest
{
    /// <summary>硫火女巫猎杀委托，追踪Boss存活与伤害贡献</summary>
    internal class SupCalHuntQuestEntry : EntrustEntryData
    {
        /// <summary>目标Boss NPC type</summary>
        public int TargetNpcType { get; init; }

        /// <summary>伤害贡献阈值，0.8=80%</summary>
        public float RequiredContribution { get; init; }

        public LocalizedText SummonHintFormat { get; init; }

        public LocalizedText ContributionFormat { get; init; }

        public LocalizedText RequiredFormat { get; init; }

        private bool isBossAlive;
        private float currentContribution;

        public SupCalHuntQuestEntry(string key, LocalizedText title, LocalizedText summary, LocalizedText category)
            : base(key, title, summary, category) { }

        //极简HUD，标题下划线与描述间5px
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
