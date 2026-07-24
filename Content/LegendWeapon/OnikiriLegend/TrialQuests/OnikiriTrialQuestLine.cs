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
            TrackerWaiting = this.GetLocalization(nameof(TrackerWaiting), () => "它还不在，等等，或我们去请");
            TrackerFighting = this.GetLocalization(nameof(TrackerFighting), () => "{0}: {1:0%}");
            TrackerBrief = this.GetLocalization(nameof(TrackerBrief), () => "下一刀：{0}");
            BossRushTargetName = this.GetLocalization(nameof(BossRushTargetName), () => "散不掉的夜");
            EventActiveFormat = this.GetLocalization(nameof(EventActiveFormat), () => "{0}: 进行中");

            TrialTitles = new LocalizedText[TRIAL_COUNT];
            //标题外号；摘要战前嘱咐口吻（对齐试炼委托点子簿）
            string[] defaultTitles = [
                "天上那档子",     //0
                "土里两股劲",     //1
                "抱成一坨的",     //2
                "听说是神",       //3
                "堵路的那档",     //4
                "硫海那股味",     //5
                "火坑还亮着",     //6
                "名字很大的",     //7
                "成对的两点",     //8
                "新旧搅一起",     //9
                "走路太像的",     //10
                "底下那朵",       //11
                "石屋那尊",       //12
                "门口念叨的",     //13
                "上面压着的",     //14
                "地心发热",       //15
                "潮里一团",       //16
                "空得慌那边",     //17
                "还在的龙",       //18
                "轰耳朵那边",     //19
                "空气发沉那边",   //20
                "又聚回来的",     //21
            ];
            for (int i = 0; i < TRIAL_COUNT; i++) {
                int idx = i;
                TrialTitles[i] = this.GetLocalization($"Trial_{i}", () => defaultTitles[idx]);
            }

            TrialSummaries = new LocalizedText[TRIAL_COUNT];
            string[] defaultSummaries = [
                "天上有档子事，我也说不准是什么\n就觉得一直被盯着，刀里也跟着发躁\n你去看看，回来告诉我，到底是什么玩意\n哦对了，别仰头仰太久，脖子会酸",
                "腐土那边两股不对劲，我分不清\n哪样先冒头你砍哪样，别两个一起盯\n刀会告诉你味不对，你先走",
                "那边秽气抱成一坨，光闻风向就腻烦\n别在边上发呆，过去处理掉\n回来先搞定比什么都要紧",
                "听说有摊东西自称神，我听着就想笑\n名字要是这么好取，谁还用干活啊\n你去核实，要是真敢这么叫，把它的名拆了\n哎，不是让你去拜，听清楚",
                "前面像堵死了，我也说不上堵的是什么\n路不通就开路\n过去以后什么感觉，回来再聊",
                "硫海那边味就不对，有东西在翻\n别让它靠岸，你去拦一拦\n回来刀要是一股味，我们再说",
                "火坑那边还亮着，像有人不肯熄\n你去弄灭，顺路看着点脚底下\n烫着了我可没法给你吹",
                "有个名字叫得很响的家伙，听着唬人\n铁里有动静，你去碰碰看，是不是虚张声势\n别跟它比谁名字大",
                "天上两点，像约好了一起出来\n成对出现的话，你分开应付，别两头挨瞪\n回来再吐槽它们长得像不像人",
                "那边新旧搅一块，看着就违和，可我还没看清\n你去分分清楚，别被唬住\n回来再说它到底哪边是真的",
                "有个影走路太像人了，我还没看清脸就先膈应\n别愣着对视，过去处理\n学谁的，回来再骂",
                "底下有朵不该开的，味就不对\n败了就处理掉，别留着烦我\n卖花的规矩回头再说，你先去",
                "石屋里有尊不爱动的，看着就硬\n弄倒就行，刀别拿去硬磕，磕了我也不舒服\n手要是麻了回来揉揉",
                "地牢门口有人念叨，像还没念完\n打断就行，省得耳朵跟着累\n念什么回来再说，反正别信",
                "月亮上面像压着什么，仰头就沉\n你去弄掉，别在那儿光愣着仰\n回来我再问你累不累",
                "地心那边发热，我这边也预热了似的\n你去，刃会烫，提前心里有数\n别在热气里杵太久，办完就撤",
                "潮气那边鬼打成一团，我懒得数\n整团处理，别一根根捡\n别在那儿过夜的话，回来我再念叨你",
                "那个方向空得慌，我这边也发沉\n我也说不上来是什么，就是不对劲\n你去，去了别一个人愣着，看不清也先顾着自己\n回来再说，活着比看清重要",
                "听说还剩这么一条龙，真假你去看\n刃大概会烫，别急着收回来\n羽什么的我不要，你人回来就行",
                "远处一轰，耳朵先不干了\n你去让它们停，顺路护好耳\n回来要是还嗡，再找安静地",
                "那边空气发沉，发滞，像门没开透\n去终止，别在门口愣着吸那口气\n回来再跟你说怎么换气",
                "它们又聚回来了，像怎么都散不净\n这场会偏长，你来，我看着\n开打前先自己站稳，回来我再啰嗦你晃不晃",
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
