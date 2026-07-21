using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using CalamityOverhaul.Content.Narrative;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.TrialQuests
{
    /// <summary>鬼切试炼线：22 段试炼注册 QuestManagerUI，按 Onikiri Level 同步状态</summary>
    internal class OnikiriTrialQuestLine : LegendTrialQuestLineBase, ILocalizedModType
    {
        public string LocalizationCategory => "Legend";

        private const int TRIAL_COUNT = 22;
        private const string KEY_PREFIX = "Onikiri_Trial_";

        public static LocalizedText QuestCategory { get; private set; }
        public static LocalizedText TrackerWaiting { get; private set; }
        public static LocalizedText TrackerFighting { get; private set; }
        public static LocalizedText TrackerBrief { get; private set; }
        public static LocalizedText BossRushTargetName { get; private set; }
        public static LocalizedText EventActiveFormat { get; private set; }
        public static LocalizedText[] TrialTitles { get; private set; }
        public static LocalizedText[] TrialSummaries { get; private set; }

        private static IReadOnlyList<LegendTrialDefinition> trials;

        public override void SetStaticDefaults() {
            QuestCategory = this.GetLocalization(nameof(QuestCategory), () => "鬼切·试炼");
            TrackerWaiting = this.GetLocalization(nameof(TrackerWaiting), () => "目标不在场，等待召唤...");
            TrackerFighting = this.GetLocalization(nameof(TrackerFighting), () => "{0}: {1:0%}");
            TrackerBrief = this.GetLocalization(nameof(TrackerBrief), () => "试刃目标：{0}");
            BossRushTargetName = this.GetLocalization(nameof(BossRushTargetName), () => "终焉之战");
            EventActiveFormat = this.GetLocalization(nameof(EventActiveFormat), () => "{0}: 进行中");

            TrialTitles = new LocalizedText[TRIAL_COUNT];
            string[] defaultTitles = [
                "不速之瞳",     //0 克苏鲁之眼
                "邪物双生",     //1 世界吞噬者/克苏鲁之脑
                "腐巢血祀",     //2 腐巢意志/血肉宿主
                "凝胶伪神",     //3 史莱姆之神
                "血墙试刃",     //4 血肉墙
                "硫海巨虫",     //5 渊海灾虫
                "硫火使者",     //6 硫磺火元素
                "钢铁长虫",     //7 毁灭者
                "双瞳裂解",     //8 双子魔眼
                "机械王颅",     //9 机械骷髅王
                "灾影摹本",     //10 灾厄之影
                "丛林妖花",     //11 世纪之花
                "遗迹石卫",     //12 石巨人
                "邪教仪典",     //13 邪教徒
                "月背斩痕",     //14 月球领主
                "地核圣火",     //15 亵渎天神
                "幽魂共生体",   //16 噬魂幽花
                "弑神之刃",     //17 神明吞噬者
                "龙裔试羽",     //18 丛林龙
                "造物巨械",     //19 星流巨械
                "至尊女巫",     //20 至尊灾厄
                "终焉乱舞",     //21 终焉之战
            ];
            for (int i = 0; i < TRIAL_COUNT; i++) {
                int idx = i;
                TrialTitles[i] = this.GetLocalization($"Trial_{i}", () => defaultTitles[idx]);
            }

            TrialSummaries = new LocalizedText[TRIAL_COUNT];
            string[] defaultSummaries = [
                "第一刀：那只悬空巨眼。斩下它，让鬼切记住何谓「试刃」。",
                "腐土巨虫与血肉之脑——择一斩核，邪气便知刀名。",
                "腐巢意志或血肉宿主，污秽聚合之物。斩尽源头。",
                "凝胶居然自称神？提纯到只剩一滩，看它还能剩什么神格。",
                "地狱横亘血肉长墙。穿过它，硬模式之门才会为刀敞开。",
                "硫磺之海浮出巨虫。拆下它的吞噬器官。",
                "熔岩深处的硫火使者。熄灭核心火焰。",
                "钢铁蠕虫第一台。切成可回收的废料。",
                "空中镜像双瞳。逐个拆除武装。",
                "戴旧王颅骨的机械末席。轰碎那颗金属头颅。",
                "女巫的克隆体在游荡。用它校准灾厄之刃的反应。",
                "丛林地下妖艳花苞已开。采集后斩落。",
                "神庙石卫等待充能。顺便试一试遗迹之铁。",
                "地牢门前狂热仪典。打断他们。",
                "月亮背面的秘密将被知晓。世界回到原来的样子。",
                "寄生地核的神明注意到了你。取得它的热能。",
                "地牢怨灵聚成共生体。记录它，然后让它归于沉寂。",
                "它吞噬神明——但我们不是神。斩之。",
                "世上仅存的龙裔。去见它，取一羽。",
                "拜访造物主的巨械。带回控制核心。",
                "女巫的存在令人困扰。终止她的混沌实验。",
                "曾被击败的敌人联合总攻。终焉乱舞，一刀定局。",
            ];
            for (int i = 0; i < TRIAL_COUNT; i++) {
                int idx = i;
                TrialSummaries[i] = this.GetLocalization($"TrialSummary_{i}", () => defaultSummaries[idx]);
            }
        }

        public override void PostSetupContent() {
            trials = LegendTrialRouteCatalog.CreateOnikiri(TrialTitles, TrialSummaries,
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
            if (NarrativeTriggerGate.IsBusy) {
                return false;
            }
            return player.HasItem(OnikiriOverride.ID);
        }

        protected override LegendData GetLegendData(Player player) => FindLegendData(player, OnikiriOverride.ID);
        protected override IEntrustEntryStyle CreateEntryStyle() => new OnikiriEntryStyle();
        protected override IEntrustTrackerWidgetStyle CreateTrackerStyle() => new OnikiriTrackerWidgetStyle();
        protected override Func<bool> CreateTrackerVisibilityCheck()
            => static () => Main.LocalPlayer.GetItem().type == OnikiriOverride.ID;

        protected override LegendTrialQuestEntry CreateTrialEntry(LegendTrialDefinition trial, int routeIndex, int routeCount) {
            var entry = new OnikiriTrialQuestEntry(KEY_PREFIX + trial.Key, trial.Title, trial.Summary, QuestCategory) {
                Trial = trial,
                Priority = routeCount - routeIndex,
                EntryStyle = CreateEntryStyle(),
                TrackerStyle = CreateTrackerStyle(),
                WaitingHint = TrackerWaiting,
                FightingFormat = TrackerFighting,
                BriefFormat = TrackerBrief,
                TrackerVisibilityCheck = CreateTrackerVisibilityCheck(),
            };
            return entry;
        }
    }
}
