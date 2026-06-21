using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke
{
    internal sealed class CampsiteChatDialogue : NarrativeScenario, ILocalizedModType
    {
        private const string MainLabel = "main";
        private const string PastLabel = "past";
        private const string ResearchLabel = "research";
        private const string ResearchDetailsLabel = "research_details";
        private const string ResearchHelpLabel = "research_help";
        private const string HistoryLabel = "history";
        private const string HistoryRuinsLabel = "history_ruins";
        private const string HistoryDangersLabel = "history_dangers";
        private const string FragmentsLabel = "fragments";
        private const string PersonalLabel = "personal";
        private const string PersonalTeaLabel = "personal_tea";
        private const string PersonalPastLabel = "personal_past";
        private const string EndLabel = "end";

        public static bool ResearchSimpleMode;
        public static bool HistorySimpleMode;

        public string LocalizationCategory => "ADV.CampsiteInteractionDialogue";

        public static LocalizedText GreetingLine { get; private set; }
        public static LocalizedText Choice_PastStory { get; private set; }
        public static LocalizedText Choice_Research { get; private set; }
        public static LocalizedText Choice_SulfurSeaHistory { get; private set; }
        public static LocalizedText Choice_AboutFragments { get; private set; }
        public static LocalizedText Choice_PersonalLife { get; private set; }
        public static LocalizedText Choice_Farewell { get; private set; }
        public static LocalizedText Choice_BackToMain { get; private set; }
        public static LocalizedText Choice_EndConversation { get; private set; }
        public static LocalizedText Past_Intro { get; private set; }
        public static LocalizedText Past_University { get; private set; }
        public static LocalizedText Past_Prime { get; private set; }
        public static LocalizedText Past_Accident { get; private set; }
        public static LocalizedText Past_Mutation { get; private set; }
        public static LocalizedText Past_Exile { get; private set; }
        public static LocalizedText Past_Reflection { get; private set; }
        public static LocalizedText Research_Intro { get; private set; }
        public static LocalizedText Research_CurrentWork { get; private set; }
        public static LocalizedText Research_Breakthrough { get; private set; }
        public static LocalizedText Research_Difficulties { get; private set; }
        public static LocalizedText Research_Theory { get; private set; }
        public static LocalizedText Choice_Research_Details { get; private set; }
        public static LocalizedText Choice_Research_Help { get; private set; }
        public static LocalizedText Choice_Research_Back { get; private set; }
        public static LocalizedText Research_Details1 { get; private set; }
        public static LocalizedText Research_Details2 { get; private set; }
        public static LocalizedText Research_Help { get; private set; }
        public static LocalizedText History_Intro { get; private set; }
        public static LocalizedText History_Origin { get; private set; }
        public static LocalizedText History_Civilization { get; private set; }
        public static LocalizedText History_Cataclysm { get; private set; }
        public static LocalizedText History_Ruins { get; private set; }
        public static LocalizedText History_Warning { get; private set; }
        public static LocalizedText Choice_History_MoreRuins { get; private set; }
        public static LocalizedText Choice_History_Dangers { get; private set; }
        public static LocalizedText Choice_History_Back { get; private set; }
        public static LocalizedText History_MoreRuins1 { get; private set; }
        public static LocalizedText History_MoreRuins2 { get; private set; }
        public static LocalizedText History_Dangers1 { get; private set; }
        public static LocalizedText History_Dangers2 { get; private set; }
        public static LocalizedText Fragments_Intro { get; private set; }
        public static LocalizedText Fragments_Nature { get; private set; }
        public static LocalizedText Fragments_Power { get; private set; }
        public static LocalizedText Fragments_Collection { get; private set; }
        public static LocalizedText Fragments_Purpose { get; private set; }
        public static LocalizedText Personal_Intro { get; private set; }
        public static LocalizedText Personal_Daily { get; private set; }
        public static LocalizedText Personal_Loneliness { get; private set; }
        public static LocalizedText Personal_Memories { get; private set; }
        public static LocalizedText Personal_Hope { get; private set; }
        public static LocalizedText Choice_Personal_Tea { get; private set; }
        public static LocalizedText Choice_Personal_Past { get; private set; }
        public static LocalizedText Choice_Personal_Back { get; private set; }
        public static LocalizedText Personal_Tea1 { get; private set; }
        public static LocalizedText Personal_Tea2 { get; private set; }
        public static LocalizedText Personal_Past1 { get; private set; }
        public static LocalizedText Personal_Past2 { get; private set; }
        public static LocalizedText Personal_Past3 { get; private set; }
        public static LocalizedText Farewell_Normal { get; private set; }
        public static LocalizedText Farewell_Friendly { get; private set; }

        public override StyleId DefaultStyle => "Sulfsea";

        public override void SetStaticDefaults() {
            GreetingLine = this.GetLocalization(nameof(GreetingLine), () => "想聊点什么？");
            Choice_BackToMain = this.GetLocalization(nameof(Choice_BackToMain), () => "换个话题");
            Choice_EndConversation = this.GetLocalization(nameof(Choice_EndConversation), () => "就这样吧");
            Choice_PastStory = this.GetLocalization(nameof(Choice_PastStory), () => "关于你现在的状态");
            Choice_Research = this.GetLocalization(nameof(Choice_Research), () => "你在拼凑什么？");
            Choice_SulfurSeaHistory = this.GetLocalization(nameof(Choice_SulfurSeaHistory), () => "这片海域的真相");
            Choice_AboutFragments = this.GetLocalization(nameof(Choice_AboutFragments), () => "关于那些残片");
            Choice_PersonalLife = this.GetLocalization(nameof(Choice_PersonalLife), () => "你的状态看起来不太好");
            Choice_Farewell = this.GetLocalization(nameof(Choice_Farewell), () => "就这样吧");
            Past_Intro = this.GetLocalization(nameof(Past_Intro), () => "那是一段被刻意抹去的过往。或者说知晓这些的鱼已经没剩几条了");
            Past_University = this.GetLocalization(nameof(Past_University), () => "那时候我们太傲慢了，妄图用科学去解释一切。直到我们在深渊底部挖出了......不合逻辑的东西。");
            Past_Prime = this.GetLocalization(nameof(Past_Prime), () => "那东西杀不死，灭不掉。它不是生物。");
            Past_Accident = this.GetLocalization(nameof(Past_Accident), () => "那起事件中没有幸存者。我被那股阴冷同化时，我做了一个癫狂的决定。");
            Past_Mutation = this.GetLocalization(nameof(Past_Mutation), () => "既然无法逃脱，那就加入。我把它强吞进了身体里。自那以后，我不算是活着，也没法死去");
            Past_Exile = this.GetLocalization(nameof(Past_Exile), () => "也好，这副身躯，刚好能镇得住这片海。");
            Past_Reflection = this.GetLocalization(nameof(Past_Reflection), () => "我只想守着这片海了，那些东西试图从深渊里出来的时候，就是我拼个彻底身死的时候。");
            Research_Intro = this.GetLocalization(nameof(Research_Intro), () => "我在试图还原一个故事，或者说，找回一种失传的经验。");
            Research_CurrentWork = this.GetLocalization(nameof(Research_CurrentWork), () => "这些残片上附着着过去的记忆。它们记录了那个古文明是如何在绝境中寻找生路的。");
            Research_Breakthrough = this.GetLocalization(nameof(Research_Breakthrough), () => "想要对抗那种恐怖，仅仅靠武力是没用的，必须找到它们的'猎杀规律'，然后加以利用。");
            Research_Difficulties = this.GetLocalization(nameof(Research_Difficulties), () => "但解读它们很危险。这些残片本身就是一种媒介，盯着它们看太久，你可能会听到不该听的声音。");
            Research_Theory = this.GetLocalization(nameof(Research_Theory), () => "如果能凑齐所有的碎片，或许我就能找到彻底封死深渊源头的方法。");
            Choice_Research_Details = this.GetLocalization(nameof(Choice_Research_Details), () => "什么叫'猎杀规律'？");
            Choice_Research_Help = this.GetLocalization(nameof(Choice_Research_Help), () => "需要我做什么？");
            Choice_Research_Back = this.GetLocalization(nameof(Choice_Research_Back), () => "听起来很疯狂");
            Research_Details1 = this.GetLocalization(nameof(Research_Details1), () => "比如，有些东西你不能看，有些名字你不能念。一旦触发了某种媒介，死亡就是必然的。那个文明试图用规则去束缚神明。");
            Research_Details2 = this.GetLocalization(nameof(Research_Details2), () => "他们甚至制造了巨大的容器，试图将那些东西关押。可惜容器漏了。");
            Research_Help = this.GetLocalization(nameof(Research_Help), () => "帮我寻找更多的碎片。我的余生都会用来研究这些");
            History_Intro = this.GetLocalization(nameof(History_Intro), () => "硫磺海只是表象。这里是那个东西的领域侵染后留下的残余。");
            History_Origin = this.GetLocalization(nameof(History_Origin), () => "这里曾是晶蓝之海，直到源头失控了。");
            History_Civilization = this.GetLocalization(nameof(History_Civilization), () => "那个古文明很强大，他们学会了利用那种诡异的力量。他们建立城市，就像是在火药桶上跳舞。");
            History_Cataclysm = this.GetLocalization(nameof(History_Cataclysm), () => "自然，平衡打破了。某种恐怖复苏了，所有活物都被瞬间抹杀，成了这片死寂的一部分。");
            History_Ruins = this.GetLocalization(nameof(History_Ruins), () => "现在你看到的这一切，不过是那场事件后的残留。海水变色是因为它死了，彻底死了。");
            History_Warning = this.GetLocalization(nameof(History_Warning), () => "如果你未来会看到死去的人向你招手，或者听到熟悉的呼唤......记住，那只是那片尸海在模仿活人。");
            Choice_History_MoreRuins = this.GetLocalization(nameof(Choice_History_MoreRuins), () => "哪里最危险？");
            Choice_History_Dangers = this.GetLocalization(nameof(Choice_History_Dangers), () => "具体有什么对策？");
            Choice_History_Back = this.GetLocalization(nameof(Choice_History_Back), () => "令人不安");
            History_MoreRuins1 = this.GetLocalization(nameof(History_MoreRuins1), () => "在那片尸海的最底层。那里有一扇门，别去敲门，门后面的东西，最好永远烂在里面。");
            History_MoreRuins2 = this.GetLocalization(nameof(History_MoreRuins2), () => "那些沉沦的废墟里还有一些没打开的房间。那是用特殊的沉重金属铸造的密室，那是为了隔绝那些东西的感知。");
            History_Dangers1 = this.GetLocalization(nameof(History_Dangers1), () => "除了那些变异的行尸走肉，还要小心看不见的东西。有时候，必死的袭击是无形的。");
            History_Dangers2 = this.GetLocalization(nameof(History_Dangers2), () => "不要相信你的眼睛，不要回应莫名的呼唤。在那个尸海里，活物才是异类。");
            Fragments_Intro = this.GetLocalization(nameof(Fragments_Intro), () => "那些残片在我眼里，是沾着血的档案。");
            Fragments_Nature = this.GetLocalization(nameof(Fragments_Nature), () => "它们是某种现象的残留物，像是被压缩的诅咒。");
            Fragments_Power = this.GetLocalization(nameof(Fragments_Power), () => "每一片碎片都是一个'媒介'。单独看或许无害，但当足够多的碎片聚集在一起时，它们会产生某种诡异力量的碰撞。");
            Fragments_Collection = this.GetLocalization(nameof(Fragments_Collection), () => "它们散落在死寂之海的各个角落，有些被埋在废墟下，有些则长在了生物的血肉里。");
            Fragments_Purpose = this.GetLocalization(nameof(Fragments_Purpose), () => "我还在拼凑它们，试图还原那场灾难的源头。只有弄清楚当初那个东西是怎么杀人的，我才能找到关押它的办法。");
            Personal_Intro = this.GetLocalization(nameof(Personal_Intro), () => "还凑活。");
            Personal_Daily = this.GetLocalization(nameof(Personal_Daily), () => "大部分时间我都在沉睡，减少思想的波动。想太多的话，会有麻烦的事找来。");
            Personal_Loneliness = this.GetLocalization(nameof(Personal_Loneliness), () => "孤独是好事。如果这周围太热闹，那一定是因为'它们'来了。我习惯了一条鱼呆着，这对我，对世界，都安全。");
            Personal_Memories = this.GetLocalization(nameof(Personal_Memories), () => "记忆已经很模糊了。有时候我分不清哪些是我的记忆，哪些是......这副身体里残留的别的个体的记忆。");
            Personal_Hope = this.GetLocalization(nameof(Personal_Hope), () => "看到你，我想起了当年的我。无知者无畏。希望你不需要像我一样，把自己变成这种样子。");
            Choice_Personal_Tea = this.GetLocalization(nameof(Choice_Personal_Tea), () => "你喝的是什么？");
            Choice_Personal_Past = this.GetLocalization(nameof(Choice_Personal_Past), () => "你还算是一条...鱼吗？");
            Choice_Personal_Back = this.GetLocalization(nameof(Choice_Personal_Back), () => "保重");
            Personal_Tea1 = this.GetLocalization(nameof(Personal_Tea1), () => "这是用深渊里的几种特殊植物熬的，味道...像是腐烂的泥土味。");
            Personal_Tea2 = this.GetLocalization(nameof(Personal_Tea2), () => "不来一杯吗？能让你冷静得像条死鱼。");
            Personal_Past1 = this.GetLocalization(nameof(Personal_Past1), () => "也许真正的我早就死了，现在的只是拥有他记忆的......另一种东西。");
            Personal_Past2 = this.GetLocalization(nameof(Personal_Past2), () => "但我还记得阳光照在脸上的感觉，还记得书本的触感。只要还记得这些，我就当自己还活着。");
            Personal_Past3 = this.GetLocalization(nameof(Personal_Past3), () => "如果没有那次事故......我大概已经安详地躺在坟墓里了吧。而不是像现在这样，想死都难。");
            Farewell_Normal = this.GetLocalization(nameof(Farewell_Normal), () => "那就这样吧，有事再来找我。");
            Farewell_Friendly = this.GetLocalization(nameof(Farewell_Friendly), () => "小心点，后生。在这个世道，能善终是一种奢望。");
        }

        public static void ResetWorldState() {
            ResearchSimpleMode = false;
            HistorySimpleMode = false;
        }

        protected override void Build(NarrativeComposer n) {
            BuildMainMenu(n);
            BuildPastBranch(n);
            BuildResearchBranch(n);
            BuildResearchDetailsBranch(n);
            BuildResearchHelpBranch(n);
            BuildHistoryBranch(n);
            BuildHistoryRuinsBranch(n);
            BuildHistoryDangersBranch(n);
            BuildFragmentsBranch(n);
            BuildPersonalBranch(n);
            BuildPersonalTeaBranch(n);
            BuildPersonalPastBranch(n);
            BuildEndBranch(n);
        }

        private static void BuildMainMenu(NarrativeComposer n) {
            n.Label(MainLabel)
             .Choice("OldDuke", GreetingLine.Value, c => c
                 .Option("past", Choice_PastStory.Value, NarrativeTarget.Goto(PastLabel))
                 .Option("research", Choice_Research.Value, NarrativeTarget.Goto(ResearchLabel))
                 .Option("history", Choice_SulfurSeaHistory.Value, NarrativeTarget.Goto(HistoryLabel))
                 .Option("fragments", Choice_AboutFragments.Value, NarrativeTarget.Goto(FragmentsLabel))
                 .Option("personal", Choice_PersonalLife.Value, NarrativeTarget.Goto(PersonalLabel))
                 .Option("farewell", Choice_Farewell.Value, NarrativeTarget.Goto(EndLabel)));
        }

        private static void BuildPastBranch(NarrativeComposer n) {
            n.Label(PastLabel)
             .Say("OldDuke", Past_Intro.Value)
             .Say("OldDuke", Past_University.Value)
             .Say("OldDuke", Past_Prime.Value)
             .Say("OldDuke", Past_Accident.Value)
             .Say("OldDuke", Past_Mutation.Value)
             .Say("OldDuke", Past_Exile.Value)
             .Choice("OldDuke", Past_Reflection.Value, c => c
                 .Option("main", Choice_BackToMain.Value, NarrativeTarget.Goto(MainLabel))
                 .Option("end", Choice_EndConversation.Value, NarrativeTarget.Goto(EndLabel)));
        }

        private static void BuildResearchBranch(NarrativeComposer n) {
            n.Label(ResearchLabel);
            if (!ResearchSimpleMode) {
                n.Say("OldDuke", Research_Intro.Value)
                 .Say("OldDuke", Research_CurrentWork.Value)
                 .Say("OldDuke", Research_Breakthrough.Value)
                 .Say("OldDuke", Research_Difficulties.Value);
            }

            ResearchSimpleMode = false;
            n.Choice("OldDuke", Research_Theory.Value, c => c
                .Option("details", Choice_Research_Details.Value, NarrativeTarget.Goto(ResearchDetailsLabel))
                .Option("help", Choice_Research_Help.Value, NarrativeTarget.Goto(ResearchHelpLabel))
                .Option("back", Choice_Research_Back.Value, NarrativeTarget.Goto(MainLabel))
                .Option("end", Choice_EndConversation.Value, NarrativeTarget.Goto(EndLabel)));
        }

        private static void BuildResearchDetailsBranch(NarrativeComposer n) {
            n.Label(ResearchDetailsLabel)
             .Say("OldDuke", Research_Details1.Value)
             .Choice("OldDuke", Research_Details2.Value, c => c
                 .Option("research", Choice_Research_Back.Value, NarrativeTarget.Goto(ResearchLabel), onSelect: () => ResearchSimpleMode = true)
                 .Option("main", Choice_BackToMain.Value, NarrativeTarget.Goto(MainLabel))
                 .Option("end", Choice_EndConversation.Value, NarrativeTarget.Goto(EndLabel)));
        }

        private static void BuildResearchHelpBranch(NarrativeComposer n) {
            n.Label(ResearchHelpLabel)
             .Choice("OldDuke", Research_Help.Value, c => c
                 .Option("research", Choice_Research_Back.Value, NarrativeTarget.Goto(ResearchLabel), onSelect: () => ResearchSimpleMode = true)
                 .Option("main", Choice_BackToMain.Value, NarrativeTarget.Goto(MainLabel))
                 .Option("end", Choice_EndConversation.Value, NarrativeTarget.Goto(EndLabel)));
        }

        private static void BuildHistoryBranch(NarrativeComposer n) {
            n.Label(HistoryLabel);
            if (!HistorySimpleMode) {
                n.Say("OldDuke", History_Intro.Value)
                 .Say("OldDuke", History_Origin.Value)
                 .Say("OldDuke", History_Civilization.Value)
                 .Say("OldDuke", History_Cataclysm.Value)
                 .Say("OldDuke", History_Ruins.Value);
            }

            HistorySimpleMode = false;
            n.Choice("OldDuke", History_Warning.Value, c => c
                .Option("ruins", Choice_History_MoreRuins.Value, NarrativeTarget.Goto(HistoryRuinsLabel))
                .Option("dangers", Choice_History_Dangers.Value, NarrativeTarget.Goto(HistoryDangersLabel))
                .Option("back", Choice_History_Back.Value, NarrativeTarget.Goto(MainLabel))
                .Option("end", Choice_EndConversation.Value, NarrativeTarget.Goto(EndLabel)));
        }

        private static void BuildHistoryRuinsBranch(NarrativeComposer n) {
            n.Label(HistoryRuinsLabel)
             .Say("OldDuke", History_MoreRuins1.Value)
             .Choice("OldDuke", History_MoreRuins2.Value, c => c
                 .Option("history", Choice_History_Back.Value, NarrativeTarget.Goto(HistoryLabel), onSelect: () => HistorySimpleMode = true)
                 .Option("main", Choice_BackToMain.Value, NarrativeTarget.Goto(MainLabel))
                 .Option("end", Choice_EndConversation.Value, NarrativeTarget.Goto(EndLabel)));
        }

        private static void BuildHistoryDangersBranch(NarrativeComposer n) {
            n.Label(HistoryDangersLabel)
             .Say("OldDuke", History_Dangers1.Value)
             .Choice("OldDuke", History_Dangers2.Value, c => c
                 .Option("history", Choice_History_Back.Value, NarrativeTarget.Goto(HistoryLabel), onSelect: () => HistorySimpleMode = true)
                 .Option("main", Choice_BackToMain.Value, NarrativeTarget.Goto(MainLabel))
                 .Option("end", Choice_EndConversation.Value, NarrativeTarget.Goto(EndLabel)));
        }

        private static void BuildFragmentsBranch(NarrativeComposer n) {
            n.Label(FragmentsLabel)
             .Say("OldDuke", Fragments_Intro.Value)
             .Say("OldDuke", Fragments_Nature.Value)
             .Say("OldDuke", Fragments_Power.Value)
             .Say("OldDuke", Fragments_Collection.Value)
             .Choice("OldDuke", Fragments_Purpose.Value, c => c
                 .Option("main", Choice_BackToMain.Value, NarrativeTarget.Goto(MainLabel))
                 .Option("end", Choice_EndConversation.Value, NarrativeTarget.Goto(EndLabel)));
        }

        private static void BuildPersonalBranch(NarrativeComposer n) {
            n.Label(PersonalLabel)
             .Say("OldDuke", Personal_Intro.Value)
             .Say("OldDuke", Personal_Daily.Value)
             .Say("OldDuke", Personal_Loneliness.Value)
             .Say("OldDuke", Personal_Memories.Value)
             .Choice("OldDuke", Personal_Hope.Value, c => c
                 .Option("tea", Choice_Personal_Tea.Value, NarrativeTarget.Goto(PersonalTeaLabel))
                 .Option("past", Choice_Personal_Past.Value, NarrativeTarget.Goto(PersonalPastLabel))
                 .Option("back", Choice_Personal_Back.Value, NarrativeTarget.Goto(MainLabel))
                 .Option("end", Choice_EndConversation.Value, NarrativeTarget.Goto(EndLabel)));
        }

        private static void BuildPersonalTeaBranch(NarrativeComposer n) {
            n.Label(PersonalTeaLabel)
             .Say("OldDuke", Personal_Tea1.Value)
             .Choice("OldDuke", Personal_Tea2.Value, c => c
                 .Option("personal", Choice_Personal_Back.Value, NarrativeTarget.Goto(PersonalLabel))
                 .Option("main", Choice_BackToMain.Value, NarrativeTarget.Goto(MainLabel))
                 .Option("end", Choice_EndConversation.Value, NarrativeTarget.Goto(EndLabel)));
        }

        private static void BuildPersonalPastBranch(NarrativeComposer n) {
            n.Label(PersonalPastLabel)
             .Say("OldDuke", Personal_Past1.Value)
             .Say("OldDuke", Personal_Past2.Value)
             .Choice("OldDuke", Personal_Past3.Value, c => c
                 .Option("personal", Choice_Personal_Back.Value, NarrativeTarget.Goto(PersonalLabel))
                 .Option("main", Choice_BackToMain.Value, NarrativeTarget.Goto(MainLabel))
                 .Option("end", Choice_EndConversation.Value, NarrativeTarget.Goto(EndLabel)));
        }

        private static void BuildEndBranch(NarrativeComposer n) {
            n.Label(EndLabel)
             .Say("OldDuke", Main.rand.NextBool() ? Farewell_Normal.Value : Farewell_Friendly.Value)
             .End();
        }
    }
}
