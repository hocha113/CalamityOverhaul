using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes
{
    /// <summary>
    /// 老公爵聊天对话
    /// </summary>
    internal class CampsiteChatDialogue : ADVScenarioBase, ILocalizedModType
    {
        public override string LocalizationCategory => "ADV.CampsiteInteractionDialogue";
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

        //角色名称
        public static LocalizedText OldDukeName { get; private set; }

        //主对话
        public static LocalizedText GreetingLine { get; private set; }

        //主选项
        public static LocalizedText Choice_PastStory { get; private set; }
        public static LocalizedText Choice_Research { get; private set; }
        public static LocalizedText Choice_SulfurSeaHistory { get; private set; }
        public static LocalizedText Choice_AboutFragments { get; private set; }
        public static LocalizedText Choice_PersonalLife { get; private set; }
        public static LocalizedText Choice_Farewell { get; private set; }

        //过去的故事分支
        public static LocalizedText Past_Intro { get; private set; }
        public static LocalizedText Past_University { get; private set; }
        public static LocalizedText Past_Prime { get; private set; }
        public static LocalizedText Past_Accident { get; private set; }
        public static LocalizedText Past_Mutation { get; private set; }
        public static LocalizedText Past_Exile { get; private set; }
        public static LocalizedText Past_Reflection { get; private set; }

        //研究内容分支
        public static LocalizedText Research_Intro { get; private set; }
        public static LocalizedText Research_CurrentWork { get; private set; }
        public static LocalizedText Research_Breakthrough { get; private set; }
        public static LocalizedText Research_Difficulties { get; private set; }
        public static LocalizedText Research_Theory { get; private set; }

        //研究子选项
        public static LocalizedText Choice_Research_Details { get; private set; }
        public static LocalizedText Choice_Research_Help { get; private set; }
        public static LocalizedText Choice_Research_Back { get; private set; }

        public static LocalizedText Research_Details1 { get; private set; }
        public static LocalizedText Research_Details2 { get; private set; }
        public static LocalizedText Research_Help { get; private set; }

        //硫磺海历史分支
        public static LocalizedText History_Intro { get; private set; }
        public static LocalizedText History_Origin { get; private set; }
        public static LocalizedText History_Civilization { get; private set; }
        public static LocalizedText History_Cataclysm { get; private set; }
        public static LocalizedText History_Ruins { get; private set; }
        public static LocalizedText History_Warning { get; private set; }

        //历史子选项
        public static LocalizedText Choice_History_MoreRuins { get; private set; }
        public static LocalizedText Choice_History_Dangers { get; private set; }
        public static LocalizedText Choice_History_Back { get; private set; }

        public static LocalizedText History_MoreRuins1 { get; private set; }
        public static LocalizedText History_MoreRuins2 { get; private set; }
        public static LocalizedText History_Dangers1 { get; private set; }
        public static LocalizedText History_Dangers2 { get; private set; }

        //关于碎片分支
        public static LocalizedText Fragments_Intro { get; private set; }
        public static LocalizedText Fragments_Nature { get; private set; }
        public static LocalizedText Fragments_Power { get; private set; }
        public static LocalizedText Fragments_Collection { get; private set; }
        public static LocalizedText Fragments_Purpose { get; private set; }

        //私人生活分支
        public static LocalizedText Personal_Intro { get; private set; }
        public static LocalizedText Personal_Daily { get; private set; }
        public static LocalizedText Personal_Loneliness { get; private set; }
        public static LocalizedText Personal_Memories { get; private set; }
        public static LocalizedText Personal_Hope { get; private set; }

        //个人生活子选项
        public static LocalizedText Choice_Personal_Tea { get; private set; }
        public static LocalizedText Choice_Personal_Past { get; private set; }
        public static LocalizedText Choice_Personal_Back { get; private set; }

        public static LocalizedText Personal_Tea1 { get; private set; }
        public static LocalizedText Personal_Tea2 { get; private set; }
        public static LocalizedText Personal_Past1 { get; private set; }
        public static LocalizedText Personal_Past2 { get; private set; }
        public static LocalizedText Personal_Past3 { get; private set; }

        //告别语
        public static LocalizedText Farewell_Normal { get; private set; }
        public static LocalizedText Farewell_Friendly { get; private set; }

        public override void SetStaticDefaults() {
            OldDukeName = this.GetLocalization(nameof(OldDukeName), () => "老公爵");

            GreetingLine = this.GetLocalization(nameof(GreetingLine), () => "想聊点什么？");

            //主选项
            Choice_PastStory = this.GetLocalization(nameof(Choice_PastStory), () => "关于你现在的状态");
            Choice_Research = this.GetLocalization(nameof(Choice_Research), () => "你在拼凑什么？");
            Choice_SulfurSeaHistory = this.GetLocalization(nameof(Choice_SulfurSeaHistory), () => "这片海域的真相");
            Choice_AboutFragments = this.GetLocalization(nameof(Choice_AboutFragments), () => "关于那些残片");
            Choice_PersonalLife = this.GetLocalization(nameof(Choice_PersonalLife), () => "你的状态看起来不太好");
            Choice_Farewell = this.GetLocalization(nameof(Choice_Farewell), () => "就这样吧");

            //过去的故事
            Past_Intro = this.GetLocalization(nameof(Past_Intro), () => "那是一段被刻意抹去的档案。或者说知晓这些的鱼已经没剩几条了");
            Past_University = this.GetLocalization(nameof(Past_University), () => "那时候我们太傲慢了，总以为手里拿着尺子就能丈量整个世界。直到我们在深渊底部挖出了某种......不符合逻辑的东西。");
            Past_Prime = this.GetLocalization(nameof(Past_Prime), () => "我们试图研究它，收容它。但我们错了，有些东西不是生物，而是一种‘现象’，一种会传染的诅咒。");
            Past_Accident = this.GetLocalization(nameof(Past_Accident), () => "没有幸存者。当我也即将被那股力量同化时，我做了一个疯狂的决定。");
            Past_Mutation = this.GetLocalization(nameof(Past_Mutation), () => "既然无法战胜，那就加入。我把它塞进了身体里。自那以后，我不算是活着，也没法死去");
            Past_Exile = this.GetLocalization(nameof(Past_Exile), () => "过也好，这副身躯，刚好能镇得住这片海。");
            Past_Reflection = this.GetLocalization(nameof(Past_Reflection), () => "这就是获得力量的代价。你必须时刻忍受着被反噬的痛苦，直到彻底失去自我的那一天。");

            //研究内容
            Research_Intro = this.GetLocalization(nameof(Research_Intro), () => "我在试图还原一个故事......或者说，找回一种失传的‘逻辑’。");
            Research_CurrentWork = this.GetLocalization(nameof(Research_CurrentWork), () => "这些残片上附着着过去的记忆。它们记录了那个古文明是如何在绝境中寻找生路的。");
            Research_Breakthrough = this.GetLocalization(nameof(Research_Breakthrough), () => "我发现了一个关键信息：想要对抗那种恐怖，仅仅靠武力是没用的，你必须找到它们的‘猎杀逻辑’，然后利用它。");
            Research_Difficulties = this.GetLocalization(nameof(Research_Difficulties), () => "但解读它们很危险。这些残片本身就是一种媒介，盯着它们看太久，你可能会听到不该听的声音。");
            Research_Theory = this.GetLocalization(nameof(Research_Theory), () => "如果能凑齐所有的碎片，或许我就能找到彻底封死深渊源头的方法......或者，彻底释放它。");

            Choice_Research_Details = this.GetLocalization(nameof(Choice_Research_Details), () => "什么叫‘猎杀逻辑’？");
            Choice_Research_Help = this.GetLocalization(nameof(Choice_Research_Help), () => "需要我做什么？");
            Choice_Research_Back = this.GetLocalization(nameof(Choice_Research_Back), () => "听起来很疯狂");

            Research_Details1 = this.GetLocalization(nameof(Research_Details1), () => "比如，有些东西你不能看，有些名字你不能念。一旦触发了某种媒介，死亡就是必然的。那个文明试图用规则去束缚神明。");
            Research_Details2 = this.GetLocalization(nameof(Research_Details2), () => "他们甚至制造了巨大的‘容器’，试图将那些东西关押进去。可惜容器漏了。");
            Research_Help = this.GetLocalization(nameof(Research_Help), () => "帮我寻找更多的碎片。但在触碰它们之前，确认一下你的影子还在不在。");

            //硫磺海历史
            History_Intro = this.GetLocalization(nameof(History_Intro), () => "硫磺海？这只是表象。这里是那个东西的领域侵染后留下的死区。");
            History_Origin = this.GetLocalization(nameof(History_Origin), () => "这里曾是晶蓝之海，直到源头失控了。那是某种更深层的侵蚀。");
            History_Civilization = this.GetLocalization(nameof(History_Civilization), () => "那个古文明很强大，他们甚至学会了利用那种诡异的力量。他们建立城市，就像是在火药桶上跳舞。");
            History_Cataclysm = this.GetLocalization(nameof(History_Cataclysm), () => "自然，平衡打破了。某种恐怖复苏了，所有活物都被瞬间抹杀，成了这片死寂的一部分。");
            History_Ruins = this.GetLocalization(nameof(History_Ruins), () => "现在你看到的这一切，不过是那场事件后的残留。海水变色是因为它死了，彻底死了。");
            History_Warning = this.GetLocalization(nameof(History_Warning), () => "在这里，常识会害死你。如果你看到死去的朋友向你招手，或者听到熟悉的呼唤......记住，那只是这片海在模仿活人。");

            Choice_History_MoreRuins = this.GetLocalization(nameof(Choice_History_MoreRuins), () => "哪里最危险？");
            Choice_History_Dangers = this.GetLocalization(nameof(Choice_History_Dangers), () => "具体有什么对策？");
            Choice_History_Back = this.GetLocalization(nameof(Choice_History_Back), () => "令人不安");

            History_MoreRuins1 = this.GetLocalization(nameof(History_MoreRuins1), () => "在那片尸海的最底层。那里有一扇门......别去敲门，门后面关着的东西，没人处理得了，除非让那个暴君来。");
            History_MoreRuins2 = this.GetLocalization(nameof(History_MoreRuins2), () => "那些沉沦的废墟里还有一些没打开的房间。那是用特殊的沉重金属铸造的密室，那是为了隔绝那些东西的感知。");
            History_Dangers1 = this.GetLocalization(nameof(History_Dangers1), () => "除了那些变异的行尸走肉，还要小心看不见的东西。有时候，必死的袭击是无形的。");
            History_Dangers2 = this.GetLocalization(nameof(History_Dangers2), () => "不要相信你的眼睛，不要回应莫名的呼唤。在那个尸海里，活人才是异类。");

            Fragments_Intro = this.GetLocalization(nameof(Fragments_Intro), () => "海洋残片在我眼里，它们是沾着血的档案。");
            Fragments_Nature = this.GetLocalization(nameof(Fragments_Nature), () => "它们是某种现象的残留物。像是被压缩的诅咒。");
            Fragments_Power = this.GetLocalization(nameof(Fragments_Power), () => "每一片碎片都是一个‘媒介’。单独看或许无害，但当足够多的碎片聚集时，它们会产生某种诡异力量的碰撞。");
            Fragments_Collection = this.GetLocalization(nameof(Fragments_Collection), () => "它们散落在死寂之海的各个角落——有些被埋在废墟下，有些则长在了生物的血肉里。");
            Fragments_Purpose = this.GetLocalization(nameof(Fragments_Purpose), () => "我还在拼凑它们，试图还原那场灾难的源头。只有弄清楚当初那个东西是怎么杀人的，我才能找到关押它的办法。");

            Personal_Intro = this.GetLocalization(nameof(Personal_Intro), () => "还凑活。");
            Personal_Daily = this.GetLocalization(nameof(Personal_Daily), () => "大部分时间我都在沉睡，减少意识的波动。情绪太激动的话，我体内的东西会......躁动不安。");
            Personal_Loneliness = this.GetLocalization(nameof(Personal_Loneliness), () => "孤独是好事。如果这周围太热闹，那一定是因为‘它们’来了。我习惯了一条鱼呆着，这对我，对世界，都安全。");
            Personal_Memories = this.GetLocalization(nameof(Personal_Memories), () => "记忆已经很模糊了。有时候我分不清哪些是我的记忆，哪些是......这副身体里残留的别的个体的记忆。");
            Personal_Hope = this.GetLocalization(nameof(Personal_Hope), () => "看到你，我想起了当年的我。无知者无畏。希望你不需要像我一样，把自己变成这种样子。");

            Choice_Personal_Tea = this.GetLocalization(nameof(Choice_Personal_Tea), () => "你喝的是什么？");
            Choice_Personal_Past = this.GetLocalization(nameof(Choice_Personal_Past), () => "你还算是一条...鱼吗？");
            Choice_Personal_Back = this.GetLocalization(nameof(Choice_Personal_Back), () => "保重");

            Personal_Tea1 = this.GetLocalization(nameof(Personal_Tea1), () => "这是用深渊里的几种特殊植物熬的，味道...像是腐烂的泥土味。");
            Personal_Tea2 = this.GetLocalization(nameof(Personal_Tea2), () => "不来一杯吗？能让你冷静得像条死鱼。");
            Personal_Past1 = this.GetLocalization(nameof(Personal_Past1), () => "谁知道呢？也许我早就死了，现在的我只是拥有老公爵记忆的一只......鬼？每次死亡都会让自身被重启，回到最开始的时候");
            Personal_Past2 = this.GetLocalization(nameof(Personal_Past2), () => "但我还记得阳光照在脸上的感觉，还记得书本的触感。只要还记得这些，我就当自己还活着。");
            Personal_Past3 = this.GetLocalization(nameof(Personal_Past3), () => "如果没有那次事故......我大概已经安详地躺在坟墓里了吧。而不是像现在这样，想死都难。");

            //告别语
            Farewell_Normal = this.GetLocalization(nameof(Farewell_Normal), () => "那就这样吧，有事再来找我。");
            Farewell_Friendly = this.GetLocalization(nameof(Farewell_Friendly), () => "小心点，后生。在这个世道，能善终是一种奢望。");
        }

        protected override void Build() {
            //注册老公爵立绘
            DialogueBoxBase.RegisterPortrait(OldDukeName.Value, OldDukeCampsite.OldDuke, OldDukeCampsite.PortraitRec, null, true);

            //主选项
            AddWithChoices(
                OldDukeName.Value,
                GreetingLine.Value,
                [
                    new Choice(Choice_PastStory.Value, Choice_Past),
                    new Choice(Choice_Research.Value, Choice_ResearchBranch),
                    new Choice(Choice_SulfurSeaHistory.Value, Choice_History),
                    new Choice(Choice_AboutFragments.Value, Choice_Fragments),
                    new Choice(Choice_PersonalLife.Value, Choice_Personal),
                    new Choice(Choice_Farewell.Value, Choice_End),
                ],
                styleOverride: () => SulfseaDialogueBox.Instance,
                choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Sulfsea
            );
        }

        //选项1：过去的故事
        private void Choice_Past() {
            ScenarioManager.Reset<CampsiteChatDialogue_Past>();
            ScenarioManager.Start<CampsiteChatDialogue_Past>();
            Complete();
        }

        internal class CampsiteChatDialogue_Past : ADVScenarioBase
        {
            public override string Key => nameof(CampsiteChatDialogue_Past);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                Add(OldDukeName.Value, Past_Intro.Value);
                Add(OldDukeName.Value, Past_University.Value);
                Add(OldDukeName.Value, Past_Prime.Value);
                Add(OldDukeName.Value, Past_Accident.Value);
                Add(OldDukeName.Value, Past_Mutation.Value);
                Add(OldDukeName.Value, Past_Exile.Value);
                Add(OldDukeName.Value, Past_Reflection.Value);
            }

            protected override void OnScenarioComplete() {
                //回到主对话
                ScenarioManager.Reset<CampsiteChatDialogue>();
                ScenarioManager.Start<CampsiteChatDialogue>();
            }
        }

        //选项2：研究内容
        private void Choice_ResearchBranch() {
            ScenarioManager.Reset<CampsiteChatDialogue_Research>();
            ScenarioManager.Start<CampsiteChatDialogue_Research>();
            Complete();
        }

        internal class CampsiteChatDialogue_Research : ADVScenarioBase
        {
            public override string Key => nameof(CampsiteChatDialogue_Research);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                Add(OldDukeName.Value, Research_Intro.Value);
                Add(OldDukeName.Value, Research_CurrentWork.Value);
                Add(OldDukeName.Value, Research_Breakthrough.Value);
                Add(OldDukeName.Value, Research_Difficulties.Value);

                //研究子选项
                AddWithChoices(
                    OldDukeName.Value,
                    Research_Theory.Value,
                    [
                        new Choice(Choice_Research_Details.Value, SubChoice_Details),
                        new Choice(Choice_Research_Help.Value, SubChoice_Help),
                        new Choice(Choice_Research_Back.Value, SubChoice_Back),
                    ],
                    styleOverride: () => SulfseaDialogueBox.Instance,
                    choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Sulfsea
                );
            }

            private void SubChoice_Details() {
                ScenarioManager.Reset<Research_Details>();
                ScenarioManager.Start<Research_Details>();
                Complete();
            }

            private void SubChoice_Help() {
                ScenarioManager.Reset<Research_HelpDialogue>();
                ScenarioManager.Start<Research_HelpDialogue>();
                Complete();
            }

            private void SubChoice_Back() {
                Complete();
                ScenarioManager.Reset<CampsiteChatDialogue>();
                ScenarioManager.Start<CampsiteChatDialogue>();
            }
        }

        internal class Research_Details : ADVScenarioBase
        {
            public override string Key => nameof(Research_Details);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                Add(OldDukeName.Value, Research_Details1.Value);
                Add(OldDukeName.Value, Research_Details2.Value);
            }

            protected override void OnScenarioComplete() {
                ScenarioManager.Reset<CampsiteChatDialogue>();
                ScenarioManager.Start<CampsiteChatDialogue>();
            }
        }

        internal class Research_HelpDialogue : ADVScenarioBase
        {
            public override string Key => nameof(Research_HelpDialogue);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                Add(OldDukeName.Value, Research_Help.Value);
            }

            protected override void OnScenarioComplete() {
                ScenarioManager.Reset<CampsiteChatDialogue>();
                ScenarioManager.Start<CampsiteChatDialogue>();
            }
        }

        //选项3：硫磺海历史
        private void Choice_History() {
            ScenarioManager.Reset<CampsiteChatDialogue_History>();
            ScenarioManager.Start<CampsiteChatDialogue_History>();
            Complete();
        }

        internal class CampsiteChatDialogue_History : ADVScenarioBase
        {
            public override string Key => nameof(CampsiteChatDialogue_History);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                Add(OldDukeName.Value, History_Intro.Value);
                Add(OldDukeName.Value, History_Origin.Value);
                Add(OldDukeName.Value, History_Civilization.Value);
                Add(OldDukeName.Value, History_Cataclysm.Value);
                Add(OldDukeName.Value, History_Ruins.Value);

                //历史子选项
                AddWithChoices(
                    OldDukeName.Value,
                    History_Warning.Value,
                    [
                        new Choice(Choice_History_MoreRuins.Value, SubChoice_Ruins),
                        new Choice(Choice_History_Dangers.Value, SubChoice_Dangers),
                        new Choice(Choice_History_Back.Value, SubChoice_Back),
                    ],
                    styleOverride: () => SulfseaDialogueBox.Instance,
                    choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Sulfsea
                );
            }

            private void SubChoice_Ruins() {
                ScenarioManager.Reset<History_Ruins_Dialogue>();
                ScenarioManager.Start<History_Ruins_Dialogue>();
                Complete();
            }

            private void SubChoice_Dangers() {
                ScenarioManager.Reset<History_Dangers_Dialogue>();
                ScenarioManager.Start<History_Dangers_Dialogue>();
                Complete();
            }

            private void SubChoice_Back() {
                Complete();
                ScenarioManager.Reset<CampsiteChatDialogue>();
                ScenarioManager.Start<CampsiteChatDialogue>();
            }
        }

        internal class History_Ruins_Dialogue : ADVScenarioBase
        {
            public override string Key => nameof(History_Ruins_Dialogue);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                Add(OldDukeName.Value, History_MoreRuins1.Value);
                Add(OldDukeName.Value, History_MoreRuins2.Value);
            }

            protected override void OnScenarioComplete() {
                ScenarioManager.Reset<CampsiteChatDialogue>();
                ScenarioManager.Start<CampsiteChatDialogue>();
            }
        }

        internal class History_Dangers_Dialogue : ADVScenarioBase
        {
            public override string Key => nameof(History_Dangers_Dialogue);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                Add(OldDukeName.Value, History_Dangers1.Value);
                Add(OldDukeName.Value, History_Dangers2.Value);
            }

            protected override void OnScenarioComplete() {
                ScenarioManager.Reset<CampsiteChatDialogue>();
                ScenarioManager.Start<CampsiteChatDialogue>();
            }
        }

        //选项4：关于碎片
        private void Choice_Fragments() {
            ScenarioManager.Reset<CampsiteChatDialogue_Fragments>();
            ScenarioManager.Start<CampsiteChatDialogue_Fragments>();
            Complete();
        }

        internal class CampsiteChatDialogue_Fragments : ADVScenarioBase
        {
            public override string Key => nameof(CampsiteChatDialogue_Fragments);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                Add(OldDukeName.Value, Fragments_Intro.Value);
                Add(OldDukeName.Value, Fragments_Nature.Value);
                Add(OldDukeName.Value, Fragments_Power.Value);
                Add(OldDukeName.Value, Fragments_Collection.Value);
                Add(OldDukeName.Value, Fragments_Purpose.Value);
            }

            protected override void OnScenarioComplete() {
                ScenarioManager.Reset<CampsiteChatDialogue>();
                ScenarioManager.Start<CampsiteChatDialogue>();
            }
        }

        //选项5：私人生活
        private void Choice_Personal() {
            ScenarioManager.Reset<CampsiteChatDialogue_Personal>();
            ScenarioManager.Start<CampsiteChatDialogue_Personal>();
            Complete();
        }

        internal class CampsiteChatDialogue_Personal : ADVScenarioBase
        {
            public override string Key => nameof(CampsiteChatDialogue_Personal);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                Add(OldDukeName.Value, Personal_Intro.Value);
                Add(OldDukeName.Value, Personal_Daily.Value);
                Add(OldDukeName.Value, Personal_Loneliness.Value);
                Add(OldDukeName.Value, Personal_Memories.Value);

                //个人生活子选项
                AddWithChoices(
                    OldDukeName.Value,
                    Personal_Hope.Value,
                    [
                        new Choice(Choice_Personal_Tea.Value, SubChoice_Tea),
                        new Choice(Choice_Personal_Past.Value, SubChoice_PastLife),
                        new Choice(Choice_Personal_Back.Value, SubChoice_Back),
                    ],
                    styleOverride: () => SulfseaDialogueBox.Instance,
                    choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Sulfsea
                );
            }

            private void SubChoice_Tea() {
                ScenarioManager.Reset<Personal_Tea_Dialogue>();
                ScenarioManager.Start<Personal_Tea_Dialogue>();
                Complete();
            }

            private void SubChoice_PastLife() {
                ScenarioManager.Reset<Personal_PastLife_Dialogue>();
                ScenarioManager.Start<Personal_PastLife_Dialogue>();
                Complete();
            }

            private void SubChoice_Back() {
                Complete();
                ScenarioManager.Reset<CampsiteChatDialogue>();
                ScenarioManager.Start<CampsiteChatDialogue>();
            }
        }

        internal class Personal_Tea_Dialogue : ADVScenarioBase
        {
            public override string Key => nameof(Personal_Tea_Dialogue);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                Add(OldDukeName.Value, Personal_Tea1.Value);
                Add(OldDukeName.Value, Personal_Tea2.Value);
            }

            protected override void OnScenarioComplete() {
                ScenarioManager.Reset<CampsiteChatDialogue>();
                ScenarioManager.Start<CampsiteChatDialogue>();
            }
        }

        internal class Personal_PastLife_Dialogue : ADVScenarioBase
        {
            public override string Key => nameof(Personal_PastLife_Dialogue);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                Add(OldDukeName.Value, Personal_Past1.Value);
                Add(OldDukeName.Value, Personal_Past2.Value);
                Add(OldDukeName.Value, Personal_Past3.Value);
            }

            protected override void OnScenarioComplete() {
                ScenarioManager.Reset<CampsiteChatDialogue>();
                ScenarioManager.Start<CampsiteChatDialogue>();
            }
        }

        //选项6：告别
        private void Choice_End() {
            ScenarioManager.Reset<CampsiteChatDialogue_End>();
            ScenarioManager.Start<CampsiteChatDialogue_End>();
            Complete();
        }

        internal class CampsiteChatDialogue_End : ADVScenarioBase
        {
            public override string Key => nameof(CampsiteChatDialogue_End);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                //随机选择告别语
                string farewell = Main.rand.NextBool() ? Farewell_Normal.Value : Farewell_Friendly.Value;
                Add(OldDukeName.Value, farewell);
            }

            protected override void OnScenarioComplete() {
                OldDukeEffect.IsActive = false;
                OldDukeEffect.Send();
            }
        }

        protected override void OnScenarioStart() {
            OldDukeEffect.IsActive = true;
        }

        protected override void OnScenarioComplete() {
            OldDukeEffect.IsActive = false;
        }
    }
}
