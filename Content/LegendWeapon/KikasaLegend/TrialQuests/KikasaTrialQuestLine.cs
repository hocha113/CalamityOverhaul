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
            TrackerWaiting = this.GetLocalization(nameof(TrackerWaiting), () => "还没入席，去请，或再等等");
            TrackerFighting = this.GetLocalization(nameof(TrackerFighting), () => "{0}: {1:0%}");
            TrackerBrief = this.GetLocalization(nameof(TrackerBrief), () => "下一席：{0}");
            BossRushTargetName = this.GetLocalization(nameof(BossRushTargetName), () => "满桌的客");
            EventActiveFormat = this.GetLocalization(nameof(EventActiveFormat), () => "{0}: 进行中");

            TrialTitles = new LocalizedText[TRIAL_COUNT];
            //标题是湖的食单外号;摘要是湖的邀约口吻(潮湿、有胃口、有耐心)
            string[] defaultTitles = [
                "第一口软的",     //0 史莱姆王
                "睁着的那颗",     //1 克苏鲁之眼
                "腐土里拱的",     //2 世吞/克脑
                "烂在地里的",     //3 腐巢/血肉宿主
                "甜的或倔的",     //4 蜂后/巨鹿
                "守门的老骨头",   //5 骷髅王
                "一大摊神",       //6 史莱姆之神
                "垒在路上的肉",   //7 血肉墙
                "上头那摊粉的",   //8 史莱姆皇后
                "馊了的长虫",     //9 渊海灾虫
                "三件铁器",       //10 三机械
                "闷着的那朵",     //11 世纪之花
                "海里的大个子",   //12 利维坦
                "方脑袋",         //13 石巨人
                "掀浪的猪",       //14 猪鲨
                "夜里发光的",     //15 光之女皇
                "念经的",         //16 拜月教徒
                "月亮后头的",     //17 月总
                "同类",           //18 幽海灵魂
                "酸水里的老东西", //19 老公爵
                "天上的长虫",     //20 神明吞噬者
                "一团火",         //21 犽戎
                "铁的和火的",     //22 星流巨械+至尊灾厄
                "满桌的时候",     //23 BossRush/始源妖龙
            ];
            for (int i = 0; i < TRIAL_COUNT; i++) {
                int idx = i;
                TrialTitles[i] = this.GetLocalization($"Trial_{i}", () => defaultTitles[idx]);
            }

            TrialSummaries = new LocalizedText[TRIAL_COUNT];
            string[] defaultSummaries = [
                "山脚有摊软的，蓝汪汪的，抖起来没完\n湖还没尝过冻子，想知道咬下去是什么响\n把它引到伞底下来，别嫌黏\n化了的话，就当是汤",
                "夜里有颗眼睛盯人，盯得很没礼貌\n湖面一照见它就皱，皱得伞骨发痒\n去，把那道视线摁进水里\n看东西的，最后都得学会闭眼",
                "腐土里拱出来的，一条长的，或一团湿脑子\n两味都腥，湖不挑，你顺路碰上哪个收哪个\n切碎的不用捡，让它自己流进来",
                "地底下烂着一个念头，或是一窝钻血的虫\n闻风向就知道，放坏了\n湖说馊的也是味，端来\n手套别摘，那汁水沾人",
                "丛林里有罐甜的，雪原里有头倔的\n你挑一样带来，湖今天两个口味都开\n甜的螫人，倔的跺脚\n哪个都别空手回来",
                "地牢门口吊着个老骨头，替人守夜守出瘾了\n骨头泡不软，湖早知道，可它就想听那声脆\n打断他的守夜，钥匙你自己留着\n骨渣沉底，正好铺路",
                "有摊冻子自称神，紫的黑的搅在一起\n湖里从不缺水，缺的是这么大的一口\n去把它放平，看它到底化成什么\n别喝，那不是给你的",
                "地狱里垒着一面肉，挡着所有人的路\n烫的，湖正好败火\n捅穿它，世界会跟着变，那不怪你\n伞面遮得住火星，放心去",
                "神圣地上头悬着摊粉冻，飞起来还带卫兵\n地上的冻子湖尝过了，会飞的还没有\n把她拽下来，连那身水晶一起\n碎晶别踩，硌伞骨",
                "硫海里泡着条长虫，一身烂鳞，半睡半醒\n那片水馊得连湖都皱眉\n把它捞干净，湖收虫，不收那口烂水\n完事把伞抖一抖再回来",
                "有人造了三件铁的：一条链子，两颗灯泡，一副骨架\n铁的沉不透，泡多久都是铁腥味\n湖不爱这口，但席上不能缺这三道\n一件一件来，别让它们同一晚都醒着",
                "丛林深处闷着一朵大的，红的，睡着\n吵醒她的人，没几个全身回来\n湖想要她的花汁，兑在水里颜色正好\n藤蔓缠伞就剪，别客气",
                "海里有位大个子，还有个唱歌的姑娘作伴\n湖对海一直有意见：凭什么它装得多\n去把大个子请来，让湖也涨一回脸\n歌别多听，会跟着走",
                "神庙里蹲着一尊石头的，方脑袋，爱蹦\n石头沉底最快，湖的收藏里正缺一尊\n拆了它的机关，壳子整个端来\n压出来的浪花，算你的赏",
                "海上有头猪，长了翅膀，还学人掀浪\n湖看不惯：水的脾气轮不到猪来使\n把它按回水里，按进哪片水，你说了算\n鱼饵我出，虫子你钓",
                "神圣地夜里飞着位发光的，脾气跟着日头走\n湖照不住她，太亮的东西水面留不下\n趁夜去，把那身光揉暗了带回来\n大白天去惹她的话，后果自己收",
                "地牢门口有个念经的，念得天都要开了\n湖听不懂经，只听得懂他要放东西进来\n打断他，让仪式烂在半截\n念珠掉了别捡，湖不收赝品",
                "月亮后头那位下来了，浑身都是眼睛\n天上的东西掉进湖里，也就是个倒影\n去，把倒影的本体收了\n那晚的月色会很好，替我看一眼",
                "地牢深处荡着一团魂，缝缝补补好几百年\n湖里泡的鬼见了它，都往伞骨里缩\n同类相残这话难听，就当是并桌\n收完这单，伞下会挤一点，忍忍",
                "硫海底下压着个老东西，一身酸气，脾气更酸\n湖跟他同行多年，谁也不服谁的水\n去替湖递个话：席面备好了，请他沉下来\n他咬人，牙口还好，你当心",
                "有条长虫在天外绕圈，绕得星星都躲\n它吞过神，湖倒想知道神是什么味\n让它钻一次不该钻的水\n壳留给你，肉归湖",
                "丛林那头养着一团火，烧了不知多少年\n火和湖是老对头，这回该做个了断\n把那身火压熄，最后一瓢湖亲自来盖\n羽毛湿了就不飘了，一根别留",
                "最后两桌一起开：一桌全是铁，一桌只有火\n工匠的得意和魔女的执念，湖都想看看沉底的样子\n先后随你，活着上桌就行\n这顿吃完，湖差不多就满了",
                "它们又都回来了，或者深渊里那条老的醒了\n满桌的宴，湖等得水位都高了\n你只管掌伞，一个一个来\n吃完这顿，雨就停了",
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
