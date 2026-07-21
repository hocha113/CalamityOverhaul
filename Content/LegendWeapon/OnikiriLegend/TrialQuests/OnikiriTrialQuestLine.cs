using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using CalamityOverhaul.Content.Scenarios.Himayo;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.TrialQuests
{
    /// <summary>鬼切试炼线,22 段注册 QuestManagerUI</summary>
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
            QuestCategory = this.GetLocalization(nameof(QuestCategory), () => "鬼切·试刃");
            TrackerWaiting = this.GetLocalization(nameof(TrackerWaiting), () => "它还不在。等等，或我们去请。");
            TrackerFighting = this.GetLocalization(nameof(TrackerFighting), () => "{0}: {1:0%}");
            TrackerBrief = this.GetLocalization(nameof(TrackerBrief), () => "下一刀：{0}");
            BossRushTargetName = this.GetLocalization(nameof(BossRushTargetName), () => "散不掉的夜");
            EventActiveFormat = this.GetLocalization(nameof(EventActiveFormat), () => "{0}: 进行中");

            TrialTitles = new LocalizedText[TRIAL_COUNT];
            //标题外号,摘要对持刀者口吻
            string[] defaultTitles = [
                "不闭眼的",       //0 克苏鲁之眼
                "腐土里的两样",   //1 世界吞噬者/克苏鲁之脑
                "抱团的秽",       //2 腐巢意志/血肉宿主
                "会走路的糖",     //3 史莱姆之神
                "挡路的肉",       //4 血肉墙
                "硫海上的虫",     //5 渊海灾虫
                "火里的那个",     //6 硫磺火元素
                "铁做的长虫",     //7 毁灭者
                "一对假眼睛",     //8 双子魔眼
                "戴颅骨的",       //9 机械骷髅王
                "学人走路的影",   //10 灾厄之影
                "开错的花",       //11 世纪之花
                "石屋里的卫",     //12 石巨人
                "门口那群人",     //13 邪教徒
                "月亮背面",       //14 月球领主
                "地心里的火",     //15 亵渎天神
                "抱成一团的鬼",   //16 噬魂幽花
                "吃神的",         //17 神明吞噬者
                "还活着的龙",     //18 丛林龙
                "造物主的铁",     //19 星流巨械
                "那个女巫",       //20 至尊灾厄
                "又聚回来的夜",   //21 终焉之战
            ];
            for (int i = 0; i < TRIAL_COUNT; i++) {
                int idx = i;
                TrialTitles[i] = this.GetLocalization($"Trial_{i}", () => defaultTitles[idx]);
            }

            TrialSummaries = new LocalizedText[TRIAL_COUNT];
            string[] defaultSummaries = [
                "天上挂着一只不眨眼的东西。看着就累。你去让它闭上吧。",
                "腐土里要么是条虫，要么是颗脑子。哪样脏，斩哪样。刀会认得味。",
                "秽气抱成一团了。源头在那儿——帮我斩断。",
                "一摊糖还自称神。名要是这么好取，我也早成神仙了。去把它的名摘掉。",
                "路被一堵肉堵住了。从中间斩过去就行。后面比这边沉。",
                "硫磺海上浮出一条虫。嘴一张就腥。别让它靠近岸。",
                "熔岩里有个还在烧的。焰灭了，那地方才能喘口气。",
                "铁做的长虫。第一台。切开就好，别跟它比谁更长。",
                "天上两只眼睛，学着一对人的样子。一只一只来。",
                "戴着旧王颅骨的铁疙瘩。头没了，剩下的只是零件。",
                "有个影子在学另一个人走路。学得越像，我越烦。帮我斩了。",
                "丛林底下开了不该开的花。花一落，地气会干净些。",
                "石屋里站着个不会眨眼的卫兵。敲醒它，或者让它睡过去。",
                "地牢门口有人在念叨。仪式还没完——打断就行。",
                "月亮背面压着什么。揭开它。世界会轻一点。",
                "地心里住着一团不肯冷的火。你去见它。刀会热，忍一下。",
                "鬼抱成了一团。别一一数，整团斩开就散了。",
                "它吃过神。我们不是神，可刀怕神的话，就只是铁。去吧。",
                "世上还剩一条龙。去见它。羽掉下来的话，捡一片给我也行。",
                "有人给自己造了铁巨人。核心在里头——取出来，它就停了。",
                "那个女巫。她在的地方，夜会发粘。去终止她。",
                "它们又聚回来了。像散不掉的夜。最长那一刀，你来。我看着。",
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

        protected override bool CanCreateEntries(Player player)
            => HimayoStorySync.CanStartOnikiriTrialQuests(player);

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
