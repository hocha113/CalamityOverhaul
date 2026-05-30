using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.TrialQuests
{
    /// <summary>
    /// 鬼妖村正试炼线——将15段试炼注册到 <see cref="QuestManagerUI"/>，
    /// 并根据 <see cref="InWorldBossPhase.Mura_Level"/> 实时同步状态。<br/>
    /// 同时显示当前进行中的试炼和所有已完成的试炼
    /// </summary>
    internal class MurasamaTrialQuestLine : LegendTrialQuestLineBase, ILocalizedModType
    {
        public string LocalizationCategory => "Legend";

        /// <summary>试炼总数（对应等级0-13的试炼，等级14为全部完成）</summary>
        private const int TRIAL_COUNT = 28;
        private const string KEY_PREFIX = "Mura_Trial_";

        #region 本地化文本

        public static LocalizedText QuestCategory { get; private set; }
        public static LocalizedText TrackerWaiting { get; private set; }
        public static LocalizedText TrackerFighting { get; private set; }
        public static LocalizedText TrackerBrief { get; private set; }

        /// <summary>每条试炼的标题</summary>
        public static LocalizedText[] TrialTitles { get; private set; }
        public static LocalizedText[] TrialSummaries { get; private set; }

        #endregion

        private static IReadOnlyList<LegendTrialDefinition> trials;

        public override void SetStaticDefaults() {
            QuestCategory = this.GetLocalization(nameof(QuestCategory), () => "鬼妖村正·试炼");
            TrackerWaiting = this.GetLocalization(nameof(TrackerWaiting), () => "目标不在场，等待召唤...");
            TrackerFighting = this.GetLocalization(nameof(TrackerFighting), () => "{0}: {1:0%}");
            TrackerBrief = this.GetLocalization(nameof(TrackerBrief), () => "猎杀目标：{0}");

            TrialTitles = new LocalizedText[TRIAL_COUNT];

            //标题风格参考MGSV:TPP，以军事行动代号的口吻
            string[] defaultTitles = [
                "沙地幽影",         //0 史莱姆王
                "沙海猎杀",         //1 荒漠灾虫
                "入侵之者",         //2 克苏鲁之眼
                "腐化清除",         //3 世吞/克脑
                "寄生威胁",         //4 腐巢意志/血肉宿主
                "地牢侵破",         //5 骷髅王
                "凝胶秽神",         //6 史莱姆之神
                "通向地狱",         //7 血肉墙
                "渊海清扫",         //8 渊海灾虫
                "硫磺净炎",         //9 硫磺火元素
                "冰原桎梏",         //10 极地冰灵
                "钢铁齿轮",         //11 毁灭者
                "双瞳歼灭",         //12 双子魔眼
                "机械王颅",         //13 机械骷髅王
                "灾影行动",         //14 灾厄之影
                "丛林之花",         //15 世纪之花
                "石化阵线",         //16 石巨人
                "瘟疫清除",         //17 瘟疫使者
                "魔像破防",         //18 毁灭魔像
                "星域终结",         //19 星神游龙
                "月球坠落",         //20 月球领主
                "幻影制裁",         //21 亵渎天神
                "幽魂祛除",         //22 噬魂幽花
                "弑神之蛇",         //23 神明吞噬者
                "丛林之炎",         //24 丛林龙犽戎
                "核心终结",         //25 星流巨械
                "混沌裁决",         //26 至尊灾厄
                "原初回归",         //27 始源妖龙
            ];
            for (int i = 0; i < TRIAL_COUNT; i++) {
                int idx = i;
                TrialTitles[i] = this.GetLocalization($"Trial_{i}", () => defaultTitles[idx]);
            }

            TrialSummaries = new LocalizedText[TRIAL_COUNT];
            string[] defaultSummaries = [
                "新兵，向我证明你自己\n那个黏糊糊的蓝胖子是个好的起点\n解决它",
                "沙海之下的海妖余孽还在游荡\n追上去，终结它",
                "夜月当空，让我们去戳爆那颗在空中乱飞的大眼球，让那个伪神彻底变成瞎子",
                "伪神的残躯玷污着泰拉的大地，去解放被腐化的大陆\n砍碎那坨伪神的大脑，剁碎那条紫色蠕虫！",
                "腐化的大地还在像心脏一样颤动，血肉的寄生者还在活动，腐化的肿瘤仍旧在思考，去将他们彻底放逐！",
                "地牢门口的诅咒已经松动，我感觉到灵能在深牢中蠢动，夹杂着惨叫、哀嚎、低语\n击碎那颗大头颅",
                "你感受到了吗？那种充斥着凝胶的恶臭腐败\n将这个污秽聚合体彻底净化",
                "走进地狱，那道横亘在熔岩上方的血肉封印就是我们通向下一个时代的门\n将它撕碎",
                "硫磺之海的弃儿阻挡了我们探寻深渊的宝藏，去终结它的吞噬和游荡",
                "熔岩深处那位硫磺使者的异端之火碍眼，去将其熄灭",
                "北境苦寒之地有一头被封印的冰雪造物\n在它彻底解封之前，将其粉碎",
                "一个巨大的钢铁蠕虫挡住了我们的征途，将它剁成碎片",
                "那双机械的眼睛俯视着我们——用刃将它们刺穿",
                "骷髅领主穿上了钢铁铠甲以为能阻止我们？击碎那颗金属骷髅头",
                "那个投入混沌的女巫，她有一个畸变的克隆姊妹，杀了那个异形，以血和火焰祭刀",
                "丛林深处那株疯狂的植物已经暴走，用刃终结这段失控的生长",
                "愚蠢的蜥蜴族只会信奉这些冥顽不灵的石头，让我们把它斩为齑粉，摧毁他们的信仰",
                "蒸汽与毒气之中有个蜜蜂机械混合体，把那个肮脏的玩意儿击落",
                "那座毁灭魔像在大地上横冲直撞，将它的钢铁躯体彻底粉碎",
                "星域的蠕虫将宇宙能量汇聚于此，终结它，这片星域才能属于我们",
                "那个躲在月亮背面的伪神不过是个残缺的拼凑物\n去斩断它的触须，挖出它的心脏，用它的血来痛饮",
                "靠吸食恒星热能苟延残喘的可怜神明，它的异端之火需要彻底熄灭",
                "地牢深处的亡灵聚合体，让那些不安的灵魂彻底归寂",
                "那条傲慢的宇宙巨蟒在世界的帷幕后蠢蠢欲动，终结他的野望",
                "丛林巨龙，泰拉大陆仅存的金源龙裔，与其他异形迥然不同，它值得我们的尊重\n然而，眼下我们却需让它再次赴死，剥夺其身上的金源魄\n这些材料将被用来打造出一套无比上乘的装备",
                "那个机械教会的异端笃信自己所创造的星流泰坦胜过神明之力\n这种信念荒谬至极，让我们将那几台泰坦归还自然的状态——一堆废铁",
                "曾拥有出色灵能天赋的女巫，本应成为守护泰拉的一大助力，却早已迷失于混沌之中\n我们唯有将她放逐，将刀锋刺入她的胸膛，让忠诚之火焚尽她腐朽的灵魂",
                "我们的征服之路早已不可阻挡，破碎那黑渊之下妖龙的铠甲\n使用那终焉之石，与异形展开最终决战",
            ];
            for (int i = 0; i < TRIAL_COUNT; i++) {
                int idx = i;
                TrialSummaries[i] = this.GetLocalization($"TrialSummary_{i}", () => defaultSummaries[idx]);
            }
        }

        public override void PostSetupContent() {
            trials = LegendTrialRouteCatalog.CreateMurasama(TrialTitles, TrialSummaries);
        }

        protected override string KeyPrefix => KEY_PREFIX;
        protected override int LegacyTrialCount => TRIAL_COUNT;
        protected override LocalizedText QuestCategoryText => QuestCategory;
        protected override LocalizedText TrackerWaitingText => TrackerWaiting;
        protected override LocalizedText TrackerFightingText => TrackerFighting;
        protected override LocalizedText TrackerBriefText => TrackerBrief;
        protected override IReadOnlyList<LegendTrialDefinition> Trials => trials;
        protected override bool CanCreateEntries(Player player) => player.HasItem(CWRID.Item_Murasama);
        protected override IEntrustEntryStyle CreateEntryStyle() => new PhantomEntryStyle();
        protected override IEntrustTrackerWidgetStyle CreateTrackerStyle() => new PhantomTrackerWidgetStyle();
        protected override Func<bool> CreateTrackerVisibilityCheck()
            => static () => Main.LocalPlayer.GetItem().type == CWRID.Item_Murasama;

        protected override LegendTrialQuestEntry CreateTrialEntry(LegendTrialDefinition trial, int routeIndex, int routeCount) {
            var entry = new MurasamaTrialQuestEntry(KEY_PREFIX + trial.Key, trial.Title, trial.Summary, QuestCategory) {
                Trial = trial,
                Priority = routeCount - routeIndex,
                EntryStyle = CreateEntryStyle(),
                TrackerStyle = CreateTrackerStyle(),
                WaitingHint = TrackerWaiting,
                FightingFormat = TrackerFighting,
                BriefFormat = TrackerBrief,
                //左侧追踪窗口仅在玩家手持鬼妖村正时显示，避免常驻打扰
                TrackerVisibilityCheck = CreateTrackerVisibilityCheck(),
            };
            return entry;
        }
    }
}
