using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.ADV.Scenarios;
using CalamityOverhaul.Content.ADV.Scenarios.Helen.Quest;
using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.TrialQuests
{
    /// <summary>比目鱼试炼线：14 段试炼注册 QuestManagerUI，按 Halibut_Level 同步状态</summary>
    internal class HalibutTrialQuestLine : LegendTrialQuestLineBase, ILocalizedModType
    {
        public string LocalizationCategory => "Legend";

        /// <summary>试炼总数（对应等级0-13的试炼，等级14为全部完成）</summary>
        private const int TRIAL_COUNT = 14;
        private const string KEY_PREFIX = "Halibut_Trial_";

        #region 本地化文本

        public static LocalizedText QuestCategory { get; private set; }
        public static LocalizedText TrackerWaiting { get; private set; }
        public static LocalizedText TrackerFighting { get; private set; }
        public static LocalizedText TrackerBrief { get; private set; }
        public static LocalizedText BossRushTargetName { get; private set; }
        public static LocalizedText EventActiveFormat { get; private set; }

        /// <summary>每条试炼的标题</summary>
        public static LocalizedText[] TrialTitles { get; private set; }

        /// <summary>每条试炼摘要文案</summary>
        public static LocalizedText[] TrialContents { get; private set; }

        #endregion

        private static IReadOnlyList<LegendTrialDefinition> trials;

        public override void SetStaticDefaults() {
            QuestCategory = this.GetLocalization(nameof(QuestCategory), () => "比目鱼传说");
            TrackerWaiting = this.GetLocalization(nameof(TrackerWaiting), () => "目标不在场，等待召唤...");
            TrackerFighting = this.GetLocalization(nameof(TrackerFighting), () => "{0}: {1:0%}");
            TrackerBrief = this.GetLocalization(nameof(TrackerBrief), () => "目标：{0}");
            BossRushTargetName = this.GetLocalization(nameof(BossRushTargetName), () => "终焉之战");
            EventActiveFormat = this.GetLocalization(nameof(EventActiveFormat), () => "{0}: 进行中");

            TrialTitles = new LocalizedText[TRIAL_COUNT];

            //试炼标题 default（按 Boss 阶段）
            string[] defaultTitles = [
                "开胃菜",           //0 史莱姆王
                "不速之瞳",         //1 克苏鲁之眼
                "丛林拜访",         //2 蜂后
                "安息与启程",       //3 骷髅王+血肉墙
                "钢铁潮汐",         //4 机械Boss/渊海灾虫
                "拙劣的复制品",     //5 灾厄之影/世纪之花
                "给遗迹塞电池",     //6 石巨人
                "月球背面",         //7 月球领主
                "冷水澡",           //8 亵渎天神
                "不屈亡魂",         //9 噬魂幽花
                "弑神者",           //10 神明吞噬者
                "升温",             //11 丛林龙
                "造物巅峰",         //12 星流巨械+至尊灾厄
                "回到海里",         //13 始源妖龙
            ];
            TrialContents = new LocalizedText[TRIAL_COUNT];
            for (int i = 0; i < TRIAL_COUNT; i++) {
                int idx = i;
                TrialTitles[i] = this.GetLocalization($"Trial_{i}", () => defaultTitles[idx]);
                TrialContents[i] = this.GetLocalization($"TextDictionary_Content_{i}", () => "");
            }
        }

        public override void PostSetupContent() {
            trials = LegendTrialRouteCatalog.CreateHalibut(TrialTitles,
                static i => TrialContents[i],
                BossRushTargetName, EventActiveFormat);
        }

        protected override string KeyPrefix => KEY_PREFIX;
        protected override int LegacyTrialCount => TRIAL_COUNT;
        protected override LocalizedText QuestCategoryText => QuestCategory;
        protected override LocalizedText TrackerWaitingText => TrackerWaiting;
        protected override LocalizedText TrackerFightingText => TrackerFighting;
        protected override LocalizedText TrackerBriefText => TrackerBrief;
        protected override IReadOnlyList<LegendTrialDefinition> Trials => trials;

        protected override bool CanCreateEntries(Player player) {
            if (ScenarioManager.IsActive()) {
                return false;
            }
            return player.HasHalibut();
        }

        protected override LegendData GetLegendData(Player player) => FindLegendData(player, HalibutOverride.ID);
        protected override IEntrustEntryStyle CreateEntryStyle() => new OceanEntryStyle();
        protected override IEntrustTrackerWidgetStyle CreateTrackerStyle() => new HalibutTrackerWidgetStyle();
        protected override Func<bool> CreateTrackerVisibilityCheck()
            => static () => Main.LocalPlayer.GetItem().type == HalibutOverride.ID;

        protected override LegendTrialQuestEntry CreateTrialEntry(LegendTrialDefinition trial, int routeIndex, int routeCount) {
            var entry = new HalibutTrialQuestEntry(KEY_PREFIX + trial.Key, trial.Title, trial.Summary, QuestCategory) {
                Trial = trial,
                Priority = routeCount - routeIndex,
                EntryStyle = CreateEntryStyle(),
                TrackerStyle = CreateTrackerStyle(),
                WaitingHint = TrackerWaiting,
                FightingFormat = TrackerFighting,
                BriefFormat = TrackerBrief,
                //左侧追踪窗口仅在玩家手持比目鱼炮时显示，避免常驻打扰
                TrackerVisibilityCheck = CreateTrackerVisibilityCheck(),
            };
            return entry;
        }
    }
}
