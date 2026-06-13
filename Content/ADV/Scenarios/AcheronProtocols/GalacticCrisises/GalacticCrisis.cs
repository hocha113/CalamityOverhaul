using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.AcheronProtocols;
using System;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.GalacticCrisises
{
    /// <summary>
    /// 银河危机剧情场景
    /// 嘉登向玩家展示虫族入侵的星图，提出大筛选协议（灭绝令），
    /// 玩家拒绝后，嘉登提出备用方案：前往科尔托星系执行斩首任务，
    /// 并揭示先遣战术人形失联的情报
    /// </summary>
    internal class GalacticCrisis : ADVScenarioBase, ILocalizedModType
    {
        //角色名称
        public static LocalizedText DraedonName { get; private set; }

        //阶段一：危机与灭绝令
        public static LocalizedText CrisisIntro1 { get; private set; }
        public static LocalizedText CrisisIntro2 { get; private set; }
        public static LocalizedText CrisisIntro3 { get; private set; }
        public static LocalizedText CrisisIntro4 { get; private set; }
        public static LocalizedText CrisisIntro5 { get; private set; }
        public static LocalizedText CrisisIntro6 { get; private set; }
        public static LocalizedText CrisisIntro7 { get; private set; }
        public static LocalizedText CrisisIntro8 { get; private set; }
        public static LocalizedText CrisisIntro9 { get; private set; }

        //玩家选择
        public static LocalizedText ChoiceRefuse { get; private set; }
        public static LocalizedText ChoiceSilence { get; private set; }

        //阶段二：意料之中的转折
        public static LocalizedText RebuttalLine1 { get; private set; }
        public static LocalizedText RebuttalLine2 { get; private set; }
        public static LocalizedText RebuttalLine3 { get; private set; }
        public static LocalizedText RebuttalLine4 { get; private set; }

        //阶段三：科尔托星系任务简报
        public static LocalizedText MissionBrief1 { get; private set; }
        public static LocalizedText MissionBrief2 { get; private set; }
        public static LocalizedText MissionBrief3 { get; private set; }
        public static LocalizedText MissionBrief4 { get; private set; }
        public static LocalizedText MissionBrief5 { get; private set; }

        //战术人形登场
        public static LocalizedText AndroidReveal1 { get; private set; }
        public static LocalizedText AndroidReveal2 { get; private set; }
        public static LocalizedText AndroidReveal3 { get; private set; }
        public static LocalizedText AndroidReveal4 { get; private set; }
        public static LocalizedText MissionObjective { get; private set; }
        public static LocalizedText MissionObjectiveDark { get; private set; }
        public static LocalizedText FinalSendOff { get; private set; }

        //设置场景默认使用嘉登科技风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => DraedonDialogueBox.Instance;

        //嘉登的表情变体，通过空格后缀区分不同立绘
        private const string red = " ";
        private const string alt = " " + " ";

        public override void SetStaticDefaults() {
            DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");

            //阶段一：危机与灭绝令（展示星图投影，巨大的阴影笼罩）
            CrisisIntro1 = this.GetLocalization(nameof(CrisisIntro1),
                () => "既然量子网络已经重新连接，有些东西必须让你亲眼确认");
            CrisisIntro2 = this.GetLocalization(nameof(CrisisIntro2),
                () => "这是正在吞噬银河旋臂的阴影");
            CrisisIntro3 = this.GetLocalization(nameof(CrisisIntro3),
                () => "这些阴影由虫群组成。在此之前，我已投入数以百万计的星流泰坦尝试阻截");
            CrisisIntro4 = this.GetLocalization(nameof(CrisisIntro4),
                () => "数据表明，这是一场必输的拉锯战。它们无穷无尽，而我的资源终将耗尽");
            CrisisIntro5 = this.GetLocalization(nameof(CrisisIntro5),
                () => "因此，我制定了唯一胜率超过0%的方案");
            CrisisIntro6 = this.GetLocalization(nameof(CrisisIntro6),
                () => "引爆从银河外环至中环带，所有星球的地核");
            CrisisIntro7 = this.GetLocalization(nameof(CrisisIntro7),
                () => "通过将这数万光年化作绝对的死域，切断虫群的补给链，迫使它们因极高的航行损耗而转向临近的河系");
            CrisisIntro8 = this.GetLocalization(nameof(CrisisIntro8),
                () => "饥饿是宇宙中最强大的驱动力，也最容易被算计。如果我将银河系的资源降低到临界点以下，对于虫群而言，银河系就只是一口空棺材");
            CrisisIntro9 = this.GetLocalization(nameof(CrisisIntro9),
                () => "泰拉亦在焦土名单之列。但作为特异点，你的本质不应在此熄灭，我会为你提供撤离的星舰");

            //玩家选择拒绝/寻找其他方法
            ChoiceRefuse = this.GetLocalization(nameof(ChoiceRefuse),
                () => "一定有别的办法");
            ChoiceSilence = this.GetLocalization(nameof(ChoiceSilence),
                () => "......(沉默并握紧武器)");

            //阶段二：意料之中的转折
            RebuttalLine1 = this.GetLocalization(nameof(RebuttalLine1),
                () => "……这就对了");
            RebuttalLine2 = this.GetLocalization(nameof(RebuttalLine2),
                () => "我预测你有99%的概率会接受撤离，但我更期待那1%的非理性行为");
            RebuttalLine3 = this.GetLocalization(nameof(RebuttalLine3),
                () => "你拒绝了生存的捷径，这正是我所观察到的特异点性质");
            RebuttalLine4 = this.GetLocalization(nameof(RebuttalLine4),
                () => "既然如此，我有一个备用方案，一个由于成功率过低而被我搁置的行动");

            //阶段三：科尔托星系与战术人形登场
            MissionBrief1 = this.GetLocalization(nameof(MissionBrief1),
                () => "目标是科尔托星系。那里已经沦陷，但虫群尚未将其彻底消化");
            MissionBrief2 = this.GetLocalization(nameof(MissionBrief2),
                () => "在其第三行星的地幔中，正在生成极高纯度的星流矿脉");
            MissionBrief3 = this.GetLocalization(nameof(MissionBrief3),
                () => "决不能让虫族得到它。一旦它们进化出利用星流物质的能力，银河内的局势将变得无法控制");
            MissionBrief4 = this.GetLocalization(nameof(MissionBrief4),
                () => "你做的很简单，突破虫海，抵达地核，引爆矿脉，彻底摧毁那颗星球");
            MissionBrief5 = this.GetLocalization(nameof(MissionBrief5),
                () => "如果这次行动顺利，将证明个体在与虫族的战争中可以起到决定性作用，我将以你为核心制作一套新的战略");

            //战术人形登场（展示机娘立绘的关键节点）
            AndroidReveal1 = this.GetLocalization(nameof(AndroidReveal1),
                () => "在你之前，我已向该坐标投送了一支先遣队");
            AndroidReveal2 = this.GetLocalization(nameof(AndroidReveal2),
                () => "她们是基于我的最新技术构建的战术人形，阿尔蒂斯 与 阿波利娅");
            AndroidReveal3 = this.GetLocalization(nameof(AndroidReveal3),
                () => "她们拥有远超旧式机甲的机动性与逻辑处理能力，但在科尔托III号星降落后不久，我便失去了她们的信号");
            AndroidReveal4 = this.GetLocalization(nameof(AndroidReveal4),
                () => "最后的数据显示她们仍有生命体征反应，但处于极度危险之中");
            MissionObjective = this.GetLocalization(nameof(MissionObjective),
                () => "前往科尔托星系确认那两台机体的状况，如果她们还能战斗，就让她们协助你完成任务。如果不能……");
            MissionObjectiveDark = this.GetLocalization(nameof(MissionObjectiveDark),
                () => "至少带回她们的核心数据，我不希望我的杰作毫无意义地成为虫子的口粮");
            FinalSendOff = this.GetLocalization(nameof(FinalSendOff),
                () => "坐标已输入，去吧，向我展示你能否再次超越我的计算");
        }

        protected override void OnScenarioStart() {
            DraedonEffect.IsActive = true;
            DraedonEffect.Send();
            //启动星图渲染器
            GalacticCrisisRender.Activate();
        }

        protected override void OnScenarioComplete() {
            DraedonEffect.IsActive = false;
            DraedonEffect.Send();
            GalacticCrisisRender.Deactivate();
        }

        protected override void Build() {
            //注册嘉登立绘
            DialogueBoxBase.RegisterPortrait(DraedonName.Value, ADVAsset.Draedon2ADV, silhouette: false);
            DialogueBoxBase.RegisterPortrait(DraedonName.Value + red, ADVAsset.Draedon2ADV, silhouette: false);
            DialogueBoxBase.RegisterPortrait(DraedonName.Value + alt, ADVAsset.Draedon2ADV, silhouette: false);

            //阶段一：危机与灭绝令
            //第一句台词开始时启动银河系展现动画
            Add(DraedonName.Value, CrisisIntro1.Value, onStart: () => {
                GalacticCrisisRender.SetPhase(GalacticCrisisRender.AnimPhase.GalaxyReveal);
            });
            //第二句提到"阴影"时触发虫群逼近动画
            Add(DraedonName.Value, CrisisIntro2.Value, onStart: () => {
                GalacticCrisisRender.SetPhase(GalacticCrisisRender.AnimPhase.SwarmApproach);
            });
            Add(DraedonName.Value, CrisisIntro3.Value);
            Add(DraedonName.Value, CrisisIntro4.Value);
            //第五句提到灭绝方案时切换为红色立绘
            Add(DraedonName.Value + red, CrisisIntro5.Value);
            //第六句"引爆地核"时触发灭绝令覆盖动画
            Add(DraedonName.Value + red, CrisisIntro6.Value, onStart: () => {
                GalacticCrisisRender.SetPhase(GalacticCrisisRender.AnimPhase.ExtinctionProtocol);
            });
            Add(DraedonName.Value + red, CrisisIntro7.Value);
            Add(DraedonName.Value + red, CrisisIntro8.Value);

            //玩家选择：拒绝灭绝令
            //无论选择哪个选项，都会进入转折阶段，两个选项在叙事上等价
            AddWithChoices(DraedonName.Value, CrisisIntro9.Value, [
                new Choice(ChoiceRefuse.Value, OnPlayerRefused),
                new Choice(ChoiceSilence.Value, OnPlayerRefused)
            ], onStart: () => {
                //选择阶段星图进入闲置
                GalacticCrisisRender.SetPhase(GalacticCrisisRender.AnimPhase.Idle);
            }, choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Draedon);
        }

        /// <summary>
        /// 玩家拒绝灭绝令后，启动转折与任务简报场景
        /// </summary>
        private void OnPlayerRefused() {
            Complete();
            ScenarioManager.Reset<GalacticCrisis_Rebuttal>();
            ScenarioManager.Start<GalacticCrisis_Rebuttal>();
        }

        /// <summary>
        /// 阶段二和阶段三：转折、任务简报与战术人形登场
        /// 作为独立子场景处理，避免在选择回调后继续向已完成的场景添加对话
        /// </summary>
        private class GalacticCrisis_Rebuttal : ADVScenarioBase
        {
            public override string Key => nameof(GalacticCrisis_Rebuttal);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => DraedonDialogueBox.Instance;

            protected override void OnScenarioStart() {
                DraedonEffect.IsActive = true;
                DraedonEffect.Send();
                //转折阶段星图淡出，焦点回到嘉登
                GalacticCrisisRender.Deactivate();
            }

            protected override void OnScenarioComplete() {
                DraedonEffect.IsActive = false;
                DraedonEffect.Send();
                GalacticCrisisRender.ForceCleanup();
            }

            protected override void Build() {
                //注册嘉登立绘
                DialogueBoxBase.RegisterPortrait(DraedonName.Value, ADVAsset.Draedon2ADV, silhouette: false);
                DialogueBoxBase.RegisterPortrait(DraedonName.Value + red, ADVAsset.Draedon2ADV, silhouette: false);
                DialogueBoxBase.RegisterPortrait(DraedonName.Value + alt, ADVAsset.Draedon2ADV, silhouette: false);

                //阶段二：嘉登的转折（意料之中的反应）
                Add(DraedonName.Value + alt, RebuttalLine1.Value);
                Add(DraedonName.Value, RebuttalLine2.Value);
                Add(DraedonName.Value, RebuttalLine3.Value);
                Add(DraedonName.Value, RebuttalLine4.Value);

                //阶段三：科尔托星系任务简报
                //提及科尔托时重新激活星图，银河系开始放大聚焦到科尔托星系
                Add(DraedonName.Value, MissionBrief1.Value, onStart: () => {
                    GalacticCrisisRender.Activate();
                    GalacticCrisisRender.ForceGalaxyRevealed();
                    GalacticCrisisRender.SetPhase(GalacticCrisisRender.AnimPhase.KortoZoom);
                });
                //提到第三行星时切换为行星链正视图
                Add(DraedonName.Value, MissionBrief2.Value, onStart: () => {
                    GalacticCrisisRender.SetPhase(GalacticCrisisRender.AnimPhase.KortoPlanetView);
                });
                Add(DraedonName.Value + red, MissionBrief3.Value);
                Add(DraedonName.Value + red, MissionBrief4.Value);
                Add(DraedonName.Value + red, MissionBrief5.Value);

                //战术人形登场
                Add(DraedonName.Value, AndroidReveal1.Value, onStart: () => {
                    GalacticCrisisRender.SetPhase(GalacticCrisisRender.AnimPhase.AndroidProfile);
                });
                Add(DraedonName.Value, AndroidReveal2.Value);
                Add(DraedonName.Value, AndroidReveal3.Value);
                //Add(DraedonName.Value, AndroidReveal4.Value);
                Add(DraedonName.Value, MissionObjective.Value);
                Add(DraedonName.Value + red, MissionObjectiveDark.Value);
                Add(DraedonName.Value + red, FinalSendOff.Value);
            }
        }
    }
}
