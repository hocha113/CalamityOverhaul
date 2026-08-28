using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using CalamityOverhaul.Content.Narrative;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.TrialQuests
{
    internal class SHPCTrialQuestLine : LegendTrialQuestLineBase, ILocalizedModType
    {
        public string LocalizationCategory => "Legend";

        private const int TRIAL_COUNT = 22;
        private const string KEY_PREFIX = "SHPC_Trial_";

        #region 本地化文本

        public static LocalizedText QuestCategory { get; private set; }
        public static LocalizedText TrackerWaiting { get; private set; }
        public static LocalizedText TrackerFighting { get; private set; }
        public static LocalizedText TrackerBrief { get; private set; }
        public static LocalizedText BossRushTargetName { get; private set; }
        public static LocalizedText EventActiveFormat { get; private set; }
        public static LocalizedText[] TrialTitles { get; private set; }
        public static LocalizedText[] TrialSummaries { get; private set; }

        #endregion

        private static IReadOnlyList<LegendTrialDefinition> trials;

        public override void SetStaticDefaults() {
            QuestCategory = this.GetLocalization(nameof(QuestCategory), () => "SHPC·试炼");
            TrackerWaiting = this.GetLocalization(nameof(TrackerWaiting), () => "目标不在场，等待召唤...");
            TrackerFighting = this.GetLocalization(nameof(TrackerFighting), () => "{0}: {1:0%}");
            TrackerBrief = this.GetLocalization(nameof(TrackerBrief), () => "采集目标：{0}");
            BossRushTargetName = this.GetLocalization(nameof(BossRushTargetName), () => "终焉之战");
            EventActiveFormat = this.GetLocalization(nameof(EventActiveFormat), () => "{0}: 进行中");

            TrialTitles = new LocalizedText[TRIAL_COUNT];
            string[] defaultTitles = [
                "眼部解剖",     //0 克苏鲁之眼
                "生化样本",     //1 世界吞噬者/克苏鲁之脑
                "腐血追猎",     //2 腐巢意志/血肉宿主
                "污秽提纯",     //3 史莱姆之神
                "封印突破",     //4 血肉墙
                "变异检疫",     //5 脓蕾沙蟒
                "硫火采样",     //6 硫磺火元素
                "机械蠕虫",     //7 毁灭者
                "双眼拆解",     //8 双子魔眼
                "钢铁颅骨",     //9 机械骷髅王
                "灾影分析",     //10 灾厄之影
                "生态考察",     //11 世纪之花
                "远古科技",     //12 石巨人
                "信息封锁",     //13 邪教徒
                "月背秘密",     //14 月球领主
                "地核探险",     //15 亵渎天神
                "幽魂观测",     //16 噬魂幽花
                "神域入侵",     //17 神明吞噬者
                "获取龙羽",     //18 丛林龙犽戎
                "造物主访问",   //19 星流巨械
                "女巫审计",     //20 至尊灾厄
                "终焉大战",     //21 终焉之战
            ];
            for (int i = 0; i < TRIAL_COUNT; i++) {
                int idx = i;
                TrialTitles[i] = this.GetLocalization($"Trial_{i}", () => defaultTitles[idx]);
            }

            TrialSummaries = new LocalizedText[TRIAL_COUNT];
            string[] defaultSummaries = [
                "朋友，复兴文明的任务终于落在了我两手上...第一个目标是那只大眼球，我需要解剖它，看看它是如何实现反重力的",
                "无论那是盘踞腐土的巨虫，还是支配血肉的大脑，去切下它们的核心",
                "邪恶生态再次涌现新的造物，污秽聚合的腐巢意志，或是嗜血成群的血肉宿主，去清除掉它",
                "凝胶居然衍生出了神？让我们对它来一次彻底的提纯分离，看看它还能剩下什么",
                "一道横亘在地狱的血肉长墙，我们需要用足够的火力轰穿这道有机屏障",
                "荒漠的沙蟒被暗影腐化成了新的变种，脓液里检出高浓度灵液\n主人，请采集它的病变组织。我需要弄清这种突变的传导路径。",
                "熔岩深处的硫磺使者正在扩散高热反应，熄灭它的核心火焰",
                "第一台机械目标是一条巨型钢铁蠕虫，把它切成可回收废料",
                "第二个机械目标是那对空中镜像眼球，逐个拆除它们的武装模块",
                "最后一台机械目标戴着旧王的颅骨，把那颗金属头颅轰碎",
                "那个女巫的克隆体在游荡，用它来校准SHPC的灾厄反应模型",
                "丛林地下有几朵妖艳的大花苞已经盛开，我们需要去采集实验资料",
                "神庙深处的远古机器人等待着一次充能启动，顺带逆向出远古科技",
                "地牢门口那群狂热的信徒正在举行某种古老的仪式，打断他们",
                "月亮背面的秘密将被我们知晓，世界将回到原来的样子",
                "寄生在地核中的神明注意到了我们，去取得它的热能利用数据",
                "地牢的怨灵聚集成了庞大的共生体，记录它，然后让它归于沉寂",
                "可以确定它不是碳基生命，它吞噬神明，但我们不是神",
                "世界上仅存的龙裔，哇这太酷了！带我去看看，我要得到它的羽毛",
                "是时候拜访我的造物主了，把星流巨械的控制核心带回来",
                "那个女巫的存在让我感到困扰，终止她的混沌实验",
                "大混战！曾经被我们击败过的敌人联合了起来，准备向我们发起总攻",
            ];
            for (int i = 0; i < TRIAL_COUNT; i++) {
                int idx = i;
                TrialSummaries[i] = this.GetLocalization($"TrialSummary_{i}", () => defaultSummaries[idx]);
            }
        }

        public override void PostSetupContent() {
            trials = LegendTrialRouteCatalog.CreateSHPC(TrialTitles, TrialSummaries,
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
            return player.HasItem(SHPCOverride.ID);
        }
        protected override LegendData GetLegendData(Player player) => FindLegendData(player, SHPCOverride.ID);
        protected override EntrustProvider Provider => EntrustProviders.SHPC;
        protected override IEntrustTrackerWidgetStyle CreateTrackerStyle() => new SHPCTrackerWidgetStyle();
        protected override Func<bool> CreateTrackerVisibilityCheck()
            => static () => Main.LocalPlayer.GetItem().type == SHPCOverride.ID;

        protected override LegendTrialQuestEntry CreateTrialEntry(LegendTrialDefinition trial, int routeIndex, int routeCount) {
            var entry = new SHPCTrialQuestEntry(KEY_PREFIX + trial.Key, trial.Title, trial.Summary, QuestCategory) {
                Trial = trial,
                Priority = routeCount - routeIndex,
                Provider = Provider,
                TrackerStyle = CreateTrackerStyle(),
                WaitingHint = TrackerWaiting,
                FightingFormat = TrackerFighting,
                BriefFormat = TrackerBrief,
                //追踪窗仅手持SHPC时显示
                TrackerVisibilityCheck = CreateTrackerVisibilityCheck(),
            };
            return entry;
        }
    }
}
