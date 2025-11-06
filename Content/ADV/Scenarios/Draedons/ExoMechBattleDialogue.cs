using CalamityMod.Events;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons
{
    /// <summary>
    /// 嘉登机甲战斗中期对话场景
    /// </summary>
    internal class ExoMechBattleDialogue : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        public string LocalizationCategory => "ADV";
        public override string Key => nameof(ExoMechBattleDialogue);

        //角色名称
        public static LocalizedText DraedonName { get; private set; }

        //战斗阶段对话
        public static LocalizedText Phase1Line1 { get; private set; }
        public static LocalizedText Phase1Line2 { get; private set; }

        public static LocalizedText Phase2Line1 { get; private set; }
        public static LocalizedText Phase2Line2 { get; private set; }

        public static LocalizedText Phase3Line1 { get; private set; }
        public static LocalizedText Phase3Line2 { get; private set; }

        public static LocalizedText Phase4Line1 { get; private set; }
        public static LocalizedText Phase4Line2 { get; private set; }

        public static LocalizedText Phase5Line1 { get; private set; }
        public static LocalizedText Phase5Line2 { get; private set; }

        public static LocalizedText Phase6Line1 { get; private set; }
        public static LocalizedText Phase6Line2 { get; private set; }
        public static LocalizedText Phase6Line3 { get; private set; }

        public static LocalizedText AresEnrageLine { get; private set; }

        //当前对话阶段
        public static int CurrentPhase { get; set; } = 0;

        //设置场景默认使用嘉登科技风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => DraedonDialogueBox.Instance;

        void IWorldInfo.OnWorldLoad()
        {
            CurrentPhase = 0;
        }

        public override void SetStaticDefaults()
        {
            DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");

            //阶段1对话
            Phase1Line1 = this.GetLocalization(nameof(Phase1Line1), () => "时间与知识的积累带来的不断改进，正是我作品精华所在。");
            Phase1Line2 = this.GetLocalization(nameof(Phase1Line2), () => "接近完美的方法，除此再无。");

            //阶段2对话
            Phase2Line1 = this.GetLocalization(nameof(Phase2Line1), () => "很好，很好，你的表现水平完全就在误差范围之内。");
            Phase2Line2 = this.GetLocalization(nameof(Phase2Line2), () => "这着实令人满意。接下来我们将进入下一个测试环节。");

            //阶段3对话
            Phase3Line1 = this.GetLocalization(nameof(Phase3Line1), () => "自我第一次到得知你的存在，我就一直在研究你的战斗，并以此让我的机械变得更强。");
            Phase3Line2 = this.GetLocalization(nameof(Phase3Line2), () => "就算是现在，我依然在检测你的行动。一切都在我的计算之内。");

            //阶段4对话
            Phase4Line1 = this.GetLocalization(nameof(Phase4Line1), () => "有趣，十分有趣。");
            Phase4Line2 = this.GetLocalization(nameof(Phase4Line2), () => "就算是面对更为困难的挑战，你仍可以稳步推进。");

            //阶段5对话
            Phase5Line1 = this.GetLocalization(nameof(Phase5Line1), () => "我依然无法理解你的本质，这样下去可不行。");
            Phase5Line2 = this.GetLocalization(nameof(Phase5Line2), () => "……我一向追求完美，可惜造化弄人，这一定是我犯下的第一个错误。");

            //阶段6对话
            Phase6Line1 = this.GetLocalization(nameof(Phase6Line1), () => "荒谬至极。");
            Phase6Line2 = this.GetLocalization(nameof(Phase6Line2), () => "我不会再让那些无用的计算干扰我对这场战斗的观察了。");
            Phase6Line3 = this.GetLocalization(nameof(Phase6Line3), () => "我将向你展示，我终极造物的全部威力。");

            //阿瑞斯狂暴对话
            AresEnrageLine = this.GetLocalization(nameof(AresEnrageLine), () => "真是愚昧，你是无法逃跑的。");
        }

        protected override void Build()
        {
            //注册嘉登立绘
            DialogueBoxBase.RegisterPortrait(DraedonName.Value, "CalamityMod/NPCs/ExoMechs/Draedon", Color.Cyan, silhouette: false);

            //根据当前阶段构建对话
            switch (CurrentPhase)
            {
                case 1:
                    Add(DraedonName.Value, Phase1Line1.Value);
                    Add(DraedonName.Value, Phase1Line2.Value);
                    break;

                case 2:
                    Add(DraedonName.Value, Phase2Line1.Value);
                    Add(DraedonName.Value, Phase2Line2.Value);
                    break;

                case 3:
                    Add(DraedonName.Value, Phase3Line1.Value);
                    Add(DraedonName.Value, Phase3Line2.Value);
                    break;

                case 4:
                    Add(DraedonName.Value, Phase4Line1.Value);
                    Add(DraedonName.Value, Phase4Line2.Value);
                    break;

                case 5:
                    Add(DraedonName.Value, Phase5Line1.Value);
                    Add(DraedonName.Value, Phase5Line2.Value);
                    break;

                case 6:
                    Add(DraedonName.Value, Phase6Line1.Value);
                    Add(DraedonName.Value, Phase6Line2.Value);
                    Add(DraedonName.Value, Phase6Line3.Value);
                    break;

                case 99://特殊：阿瑞斯狂暴
                    Add(DraedonName.Value, AresEnrageLine.Value);
                    break;
            }
        }

        /// <summary>
        /// 触发特定阶段的对话
        /// </summary>
        public static void TriggerPhaseDialogue(int phase)
        {
            CurrentPhase = phase;
            ScenarioManager.Start<ExoMechBattleDialogue>();
        }

        /// <summary>
        /// 触发阿瑞斯狂暴对话
        /// </summary>
        public static void TriggerAresEnrage()
        {
            CurrentPhase = 99;
            ScenarioManager.Start<ExoMechBattleDialogue>();
        }
    }
}
