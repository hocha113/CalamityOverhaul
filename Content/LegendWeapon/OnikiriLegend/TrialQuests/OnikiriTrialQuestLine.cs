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
            TrackerWaiting = this.GetLocalization(nameof(TrackerWaiting), () => "它还不在");
            TrackerFighting = this.GetLocalization(nameof(TrackerFighting), () => "{0}: {1:0%}");
            TrackerBrief = this.GetLocalization(nameof(TrackerBrief), () => "下一刀：{0}");
            BossRushTargetName = this.GetLocalization(nameof(BossRushTargetName), () => "终焉之战");
            EventActiveFormat = this.GetLocalization(nameof(EventActiveFormat), () => "{0}: 进行中");

            TrialTitles = new LocalizedText[TRIAL_COUNT];
            //标题外号；摘要战前嘱咐口吻（对齐试炼委托点子簿）
            string[] defaultTitles = [
                "天穹夜目",     //0 克苏鲁之眼
                "秽土双孽",     //1 世吞/克脑
                "结聚秽巢",     //2 腐巢/血肉宿主
                "妄称神明",     //3 史莱姆之神
                "截界肉壁",     //4 血肉墙
                "腐沙脓鳞",     //5 脓蕾沙蟒
                "深渊余烬",     //6 硫磺火元素
                "轰鸣铁蛇",     //7 毁灭者
                "双生魔瞳",     //8 双子魔眼
                "新旧残骸",     //9 机械骷髅王
                "拟形魔影",     //10 灾厄克隆体
                "丛林幽葩",     //11 世纪之花
                "神庙顽尊",     //12 石巨人
                "古门狂祷",     //13 拜月教徒
                "重月倾坠",     //14 月球领主
                "地心炽辉",     //15 亵渎天神
                "阴潮怨簇",     //16 噬魂幽花
                "虚空噬界",     //17 神明吞噬者
                "遗世炎龙",     //18 犽戎
                "星流震鸣",     //19 星流巨械
                "窒息灾岚",     //20 至尊灾厄
                "残夜重聚",     //21 BossRush/终焉之战
            ];
            for (int i = 0; i < TRIAL_COUNT; i++) {
                int idx = i;
                TrialTitles[i] = this.GetLocalization($"Trial_{i}", () => defaultTitles[idx]);
            }

            TrialSummaries = new LocalizedText[TRIAL_COUNT];
            string[] defaultSummaries = [
                "天上总觉得有东西在盯着看，刀里也跟着发慌\n我也说不准是什么，你上去瞧一眼\n别一直死死仰着头，脖子酸得很，打完了回来同我说说",
                "腐土那边传来的风味儿很冲，又腥又重，像是藏了两股东西\n要是碰上了，哪边先扑过来就先打哪个，别两头一起顾\n闻着不对劲就快点拔刀，早点打完早点出来透气",
                "地下那块儿的秽气聚成一团了，隔老远都觉得黏糊糊的\n一直放着不管肯定更麻烦，你去把它打散了吧\n泥地里走动看着点脚下，别踩得满身都是泥",
                "听人说那边有团烂泥自称是神仙，我听着都想笑\n名字要是这么好取，哪还用得着辛苦修行啊\n你去看看它到底有多大能耐，要是真在装神弄鬼，就一刀给它戳破\n听好了呀，是去戳破它，可别犯傻跟着去拜",
                "前面地底深处像被一堵大肉墙死死堵上了，刀划过去都觉得发闷\n路不通就只能劈开了\n打完这一仗，周围天地大概要变样了，打起精神来，等你过关了回来再聊",
                "沙漠底下动静很大，沙子翻来覆去的，像是有什么大虫子在拼命往下钻\n趁它还没钻深，你去拦一下\n出刀看准点，别把整把刀全插进泥浆里，我可还在刀里住着呢，给我留点干净地方",
                "火坑那边还红着呢，隔这么远刀身都觉得烫\n你去给它弄灭了吧，省得一直烧着\n路过岩浆滩看着点脚底下，烫着了可没人替你吹",
                "地下传来的动静一下比一下响，刀都在跟着震，听着是个浑身是铁的大家伙\n名字叫得再吓人，铁块终归是铁块，你去碰碰看它有多硬\n别跟那硬铁死磕，找空隙砍，免得把手震麻了",
                "天上有两个亮斑像约好了一样一块儿出来，被两边同时盯着怪难受的\n它们成双成对的不好对付，你扯开距离一个一个打，别夹在中间两头挨揍\n快去吧，动作利索点，回来再跟我说说它们到底长啥样",
                "那边传来的动静怪怪的，铁架子混着碎骨头，半生不熟地拼在一块儿\n架子搭得再大也是拼出来的，别被它的阵势给唬住了\n找衔接的地方下刀，把它拆散了，打完回来告诉我哪块才是真的",
                "前面有个影子走起路来太像人了，可身上一点活人的气味都没有\n越是学人走路的假东西越古怪，可别愣在那儿跟它干瞪眼\n过去拔刀打碎它，到底是学谁的，打完了再说",
                "丛林底下有股香味飘上来了，甜丝丝的，闻着却透着股衰败的蔫味\n花开得再好看，既然蔫了坏了就得剪掉，免得烂成一团\n修花剪枝的事等回来再慢慢跟你说，快去吧，走路小心脚下藤蔓",
                "石屋里坐着个不爱挪窝的大石块，看着就很沉很结实\n想办法把它推倒弄散架就好，刀可别去硬碰硬死磕，磕崩了我也不好受\n要是手腕震酸了，打完回来好好甩甩",
                "地牢门口有群人一直叽里咕噜念叨个没完，听得耳朵都累了\n直接过去打断他们，省得一直吵人\n不管嘴里念的是什么乱七八糟的，一句都别信，打完早点回来",
                "天顶上像是压着一块好大的阴云，仰着头都觉得胸闷\n既然挡在上面碍事，就上去把它打掉\n别光站在下面发愣，认真点对付，等你打完回来了我再问你累不累",
                "地心那边一阵一阵往外冒热气，我寄身的刀刃都被烤得发烫了\n这一趟离近了肯定热得要命，你提前心里有个数\n别在滚烫的热气里死耗着，打完就赶紧撤出来",
                "底下潮乎乎的，聚了一大窝怨魂在打转，多得我都懒得数了\n既然全挤在一堆，那就别一只只挑了，看准了一刀扫平\n地牢里又冷又潮，可别在那儿待太久，打完了就赶紧回来",
                "那个方向空荡荡的，心里直发慌，刀身也跟着往下沉\n我也说不准迎面过来的是个什么大怪物，总之千万小心\n到了那边别看呆了发愣，不管多险，你平平安安回来比什么都强",
                "听说火海尽头还留着这么一条大龙，是真是假你去看看\n动起手来刀大概会被火烤得很烫，你多忍着点\n那些稀罕的龙鳞龙羽我都不在乎，只要你全须全尾地回来就行",
                "老远就听见铁炮哐哐乱响，吵得我脑仁都疼\n快去让那群铁家伙消停下来，路上护好自己的耳朵\n回来要是耳朵还嗡嗡叫，我再陪你找个安静地方歇着",
                "那边空气黏糊糊的发沉，像吸一口都堵在胸口上\n去给它划开吧，可别傻站在风口里硬吸那股闷气\n打完了回来，我再教你怎么顺气",
                "它们像赶不干净似的，又全挤到一块儿来了\n这一场怕是要打好久，放开手脚去吧，我就在刀里看着你\n出招先把脚底下站稳了，打完了回来我再唠叨你刚才晃没晃",
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
        protected override EntrustProvider Provider => EntrustProviders.Onikiri;
        protected override IEntrustTrackerWidgetStyle CreateTrackerStyle() => new OnikiriTrackerWidgetStyle();
        protected override Func<bool> CreateTrackerVisibilityCheck()
            => static () => Main.LocalPlayer.GetItem().type == OnikiriOverride.ID;

        protected override LegendTrialQuestEntry CreateTrialEntry(LegendTrialDefinition trial, int routeIndex, int routeCount) {
            var entry = new OnikiriTrialQuestEntry(KEY_PREFIX + trial.Key, trial.Title, trial.Summary, QuestCategory) {
                Trial = trial,
                Priority = routeCount - routeIndex,
                Provider = Provider,
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
