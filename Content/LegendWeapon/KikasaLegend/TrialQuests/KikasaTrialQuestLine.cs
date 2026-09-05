using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using CalamityOverhaul.Content.Narrative;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.TrialQuests
{
    /// <summary>
    /// 鬼伞沉宴试炼线,24 段注册 QuestManagerUI。
    /// 委托人是湖本身，阴湿、有胃口、有耐心,每一关都是一道待沉的席
    /// </summary>
    internal class KikasaTrialQuestLine : LegendTrialQuestLineBase, ILocalizedModType
    {
        public string LocalizationCategory => "Legend";

        private const int TRIAL_COUNT = 24;
        private const string KEY_PREFIX = "Kikasa_Trial_";

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
            QuestCategory = this.GetLocalization(nameof(QuestCategory), () => "鬼伞·沉宴");
            TrackerWaiting = this.GetLocalization(nameof(TrackerWaiting), () => "还没入席");
            TrackerFighting = this.GetLocalization(nameof(TrackerFighting), () => "{0}: {1:0%}");
            TrackerBrief = this.GetLocalization(nameof(TrackerBrief), () => "下一席：{0}");
            BossRushTargetName = this.GetLocalization(nameof(BossRushTargetName), () => "终焉之战");
            EventActiveFormat = this.GetLocalization(nameof(EventActiveFormat), () => "{0}: 进行中");

            TrialTitles = new LocalizedText[TRIAL_COUNT];
            //标题是湖的食单外号;摘要是湖的邀约口吻(潮湿、有胃口、有耐心)
            string[] defaultTitles = [
                "初席软冻",     //0 史莱姆王
                "夜穹窥目",     //1 克苏鲁之眼
                "腐壤双腥",     //2 世吞/克脑
                "地底陈馊",     //3 腐巢/血肉宿主
                "甘浆与倔蹄",   //4 蜂后/巨鹿
                "门前枯骨",     //5 骷髅王
                "驳杂神胶",     //6 史莱姆之神
                "横途热肉",     //7 血肉墙
                "展翼粉冻",     //8 史莱姆皇后
                "腐沙金涎",     //9 脓蕾沙蟒
                "生铁三味",     //10 三机械
                "雨林红蕊",     //11 世纪之花
                "晶壳弹肉",     //12 渊晶海虾(路线序在石巨人后,文案下标沿用旧利维坦席)
                "神庙顽石",     //13 石巨人(路线序在渊晶海虾前)
                "弄潮翼彘",     //14 猪鲨
                "夜空流光",     //15 光之女皇
                "门前残诵",     //16 拜月教徒
                "月背倒影",     //17 月总
                "同类并桌",     //18 幽海灵魂
                "酸海老宿",     //19 老公爵
                "穹外噬神",     //20 神明吞噬者
                "不熄烈羽",     //21 犽戎
                "极铁与极火",   //22 星流巨械+至尊灾厄
                "满席终宴",     //23 BossRush/始源妖龙
            ];
            for (int i = 0; i < TRIAL_COUNT; i++) {
                int idx = i;
                TrialTitles[i] = this.GetLocalization($"Trial_{i}", () => defaultTitles[idx]);
            }

            TrialSummaries = new LocalizedText[TRIAL_COUNT];
            string[] defaultSummaries = [
                "山底下摊着团蓝汪汪的软泥，抖个不停\n也不知从哪聚起来的浮肿壳子，虚张声势罢了\n去把它打散了，别让它总在路上碍事",
                "夜里半空总悬着只大眼睛，直勾勾盯人\n眨都不眨一下，看得人心里发毛\n去给它打落下来，闭上了眼睛，倒也省得惹人心烦",
                "腐土里钻出来的两条孽物，一个长虫，一个湿脑子\n隔着老远都能闻到一股刺鼻的泥腥味\n你顺路碰上哪个就先收拾哪个，切碎了别沾一身腥回来",
                "地底下烂着一团烂肉巢穴，生了满窝钻血的小虫子\n放着不管只怕越烂越深，直接过去打烂它\n手套戴严实些，那些汁水沾在皮肉上可不好洗",
                "雨林里藏着只毒蜂，雪原里有头瞎了一只眼的倔鹿\n挑先撞见的那只打了就好，脾气一个比一个冲\n蜂针带毒，残蹄带风雪，哪边都别大意了",
                "地牢门口吊着个看门的老骨头，守了一辈子还舍不得撒手\n一把年纪了，执念倒比铁锁还重\n去打断他的守夜，钥匙拿走，送他入土歇着吧",
                "紫的黑的搅成一锅烂胶，嘴上倒敢自称是神\n驳杂散乱，终究成不了什么大气候\n去把它打碎化开，免得留在这儿脏了地方",
                "地狱里横着好大一面肉墙，把前路全给堵死了\n隔着伞幕都觉得热气逼人，烘得难受\n捅穿它就是了，世界要是跟着变了样，那也不关你的事",
                "半空里悬着团粉粉嫩嫩的果冻，出门打架还带着侍从\n花里胡哨的，排场摆得倒是不小\n把它打下来吧，排场再大，也经不住一场雨淋",
                "沙子里钻出一条生满烂花的大虫，淌了一地的金脓\n看着比水里泡烂的还恶心些，凑近闻一口都嫌呛人\n去把它打发了，完事把伞面冲干净再回来",
                "不知道谁造了三件铁皮机括，链子、灯泡，还有副四臂钢架\n生铁腥气重得很，吵吵闹闹的\n一件一件拆，别让他们聚在一块儿折腾",
                "雨林深处睡着一朵大红花，藤蔓到处疯长\n吵醒了它脾气可大得很，见人就缠\n去把它连根拔了，藤蔓缠上伞就直接割断",
                "海滩底下窝着只大虾，披着身亮晶晶的硬壳，还会蜕皮\n挥起钳子一拳能把水打散，动静倒是不小\n壳随它去换，出拳看仔细些，别冷不丁挨上一记",
                "神庙里蹲着个方脑袋的大石墩子，半天不挪一下窝\n机关算计得挺精细，砸开了看也不过是个空心架子\n去把它拆散架了，省得占着石室挡道",
                "海里有头长了翅膀的猪，也学着人掀风作浪\n也不撒泡尿照照，水的脾气哪轮得到一头海猪来摆弄\n给它按回深水里去，省得总在眼前扑腾",
                "夜空里飞着个浑身发光的家伙，大半夜的刺眼得很\n我不喜欢太晃眼的东西，看着费劲\n趁夜色去把它那身光揉暗了，大白天可别去招惹她",
                "地牢门口有人念念叨叨，听得人耳朵发胀\n他想从虚空里引落的东西，不是什么好路数\n打断那场仪式，免得把动静闹得收拾不住",
                "月亮后头那个大影子要落下来了，浑身长满眼睛\n盯着人间看了这么多年，也该到了结的时候了\n去把它打散，打完今晚的月色，替我多看一眼",
                "地牢深处荡着好大一团怨魂，缝缝补补纠缠了几百年\n跟我算是同一路东西，终日哀哀戚戚的，看着都嫌累\n去让它们彻底散了吧，归于清静，总好过在那儿受折磨",
                "硫海底下压着个老不死的东西，浑身酸气，脾气比酸水还冲\n当年跟我打过照面，谁也没让过谁半步\n去帮我了结了他，替我记他一笔",
                "天上盘旋的那条大长虫，传闻连神明都吞过\n神到了什么层次我不在意，但它在头顶绕圈，看得让人眼晕\n把它打下来，那身硬甲你留着，其余的不必理会",
                "丛林那头烧着一团烈火，多少年了都没见熄过\n烈火与风雨，自古就势不两立\n去把那身火扑灭了，羽毛淋湿了就飘不起来，正好",
                "最后两场硬仗凑在一块儿了，一帮是铁造的傀儡，一个是执念未散的魔女\n两边都不是省油的灯，闹得这方天地没一刻消停\n先后随你挑，挑利索的先打，活着走出来就行",
                "曾经倒下的那些东西又重新聚齐了，亦或是深渊底下的老龙醒了\n不管是哪种，阵仗都绝不会小\n握紧手里的伞，一步一步来，这场打完……外头的雨也该停了",
            ];
            for (int i = 0; i < TRIAL_COUNT; i++) {
                int idx = i;
                TrialSummaries[i] = this.GetLocalization($"TrialSummary_{i}", () => defaultSummaries[idx]);
            }
        }

        public override void PostSetupContent() {
            trials = LegendTrialRouteCatalog.CreateKikasa(TrialTitles, TrialSummaries,
                BossRushTargetName, EventActiveFormat);
        }

        protected override string KeyPrefix => KEY_PREFIX;
        protected override int LegacyTrialCount => TRIAL_COUNT;
        protected override LocalizedText QuestCategoryText => QuestCategory;
        protected override LocalizedText TrackerWaitingText => TrackerWaiting;
        protected override LocalizedText TrackerFightingText => TrackerFighting;
        protected override LocalizedText TrackerBriefText => TrackerBrief;
        protected override IReadOnlyList<LegendTrialDefinition> Trials => trials;

        //拿到伞就开线,不吃剧情门(演出忙时暂缓注册,镜像 SHPC)
        protected override bool CanCreateEntries(Player player) {
            if (NarrativeTriggerGate.IsBusy) {
                return false;
            }
            return player.HasItem(KikasaOverride.ID);
        }

        protected override LegendData GetLegendData(Player player) => FindLegendData(player, KikasaOverride.ID);
        protected override EntrustProvider Provider => EntrustProviders.Kikasa;
        protected override IEntrustTrackerWidgetStyle CreateTrackerStyle() => new KikasaTrackerWidgetStyle();
        protected override Func<bool> CreateTrackerVisibilityCheck()
            => static () => Main.LocalPlayer.GetItem().type == KikasaOverride.ID;

        protected override LegendTrialQuestEntry CreateTrialEntry(LegendTrialDefinition trial, int routeIndex, int routeCount) {
            var entry = new KikasaTrialQuestEntry(KEY_PREFIX + trial.Key, trial.Title, trial.Summary, QuestCategory) {
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
