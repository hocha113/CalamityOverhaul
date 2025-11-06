using System;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons
{
    /// <summary>
    /// 嘉登战斗结束对话场景
    /// </summary>
    internal class ExoMechEndingDialogue : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        public string LocalizationCategory => "ADV";
        public override string Key => nameof(ExoMechEndingDialogue);

        //角色名称
        public static LocalizedText DraedonName { get; private set; }

        //结束对话
        public static LocalizedText EndLine1 { get; private set; }
        public static LocalizedText EndLine2 { get; private set; }
        public static LocalizedText EndLine3 { get; private set; }
        public static LocalizedText EndLine4 { get; private set; }
        public static LocalizedText EndLine5 { get; private set; }
        public static LocalizedText EndLine6 { get; private set; }
        public static LocalizedText EndLine7 { get; private set; }
        public static LocalizedText EndLine8 { get; private set; }
        public static LocalizedText EndLine9 { get; private set; }

        //玩家尝试击杀嘉登时的对话
        public static LocalizedText KillAttemptLine { get; private set; }

        //是否为击杀尝试场景
        public static bool IsKillAttempt { get; set; } = false;

        //设置场景默认使用嘉登科技风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => DraedonDialogueBox.Instance;

        void IWorldInfo.OnWorldLoad() {
            IsKillAttempt = false;
        }

        public override void SetStaticDefaults() {
            DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");

            //结束对话(对应原游戏的 DraedonEndText 系列)
            EndLine1 = this.GetLocalization(nameof(EndLine1), () => "一个未知因素——你，是一个特异点。");
            EndLine2 = this.GetLocalization(nameof(EndLine2), () => "你对这片大地和它的历史而言，只是外来之人，就和我一样。");
            EndLine3 = this.GetLocalization(nameof(EndLine3), () => "……很抱歉，但在看了这样一场\"展示\"之后，我必须得花点时间整理我的思绪。");
            EndLine4 = this.GetLocalization(nameof(EndLine4), () => "迄今为止喷洒的血液已经让这片大陆变得陈腐无比，毫无生气。");
            EndLine5 = this.GetLocalization(nameof(EndLine5), () => "你也挥洒了自己的鲜血，但这可能足以带来一个新的时代……是什么，我不知道。但那一定是我所渴望看到的时代。");
            EndLine6 = this.GetLocalization(nameof(EndLine6), () => "现在，你想要接触那位暴君。可惜我无法帮到你。");
            EndLine7 = this.GetLocalization(nameof(EndLine7), () => "这并非出自怨恨，毕竟从一开始，我的目标就只有观察刚才的这一场战斗。");
            EndLine8 = this.GetLocalization(nameof(EndLine8), () => "但你过去也成功过，所以你最后会找到办法的。");
            EndLine9 = this.GetLocalization(nameof(EndLine9), () => "我必须尊重并承认你的胜利，但现在，我得把注意力放回到我的机械上了。");

            //击杀尝试对话
            KillAttemptLine = this.GetLocalization(nameof(KillAttemptLine), () => "……你的行为没什么必要。");
        }

        protected override void Build() {
            //注册嘉登立绘
            DialogueBoxBase.RegisterPortrait(DraedonName.Value, "CalamityMod/NPCs/ExoMechs/Draedon", Color.Cyan, silhouette: false);

            if (IsKillAttempt) {
                //玩家尝试击杀嘉登
                Add(DraedonName.Value, KillAttemptLine.Value);
            }
            else {
                //正常结束对话
                Add(DraedonName.Value, EndLine1.Value);
                Add(DraedonName.Value, EndLine2.Value);
                Add(DraedonName.Value, EndLine3.Value);
                Add(DraedonName.Value, EndLine4.Value);
                Add(DraedonName.Value, EndLine5.Value);
                Add(DraedonName.Value, EndLine6.Value);
                Add(DraedonName.Value, EndLine7.Value);
                Add(DraedonName.Value, EndLine8.Value);
                Add(DraedonName.Value, EndLine9.Value);
            }
        }

        protected override void OnScenarioComplete() {
            //对话结束后的逻辑
            IsKillAttempt = false;
        }

        /// <summary>
        /// 触发正常结束对话
        /// </summary>
        public static void TriggerEndingDialogue() {
            IsKillAttempt = false;
            ScenarioManager.Start<ExoMechEndingDialogue>();
        }

        /// <summary>
        /// 触发击杀尝试对话
        /// </summary>
        public static void TriggerKillAttemptDialogue() {
            IsKillAttempt = true;
            ScenarioManager.Start<ExoMechEndingDialogue>();
        }
    }
}
