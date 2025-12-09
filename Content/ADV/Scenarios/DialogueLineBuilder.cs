using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.ADV.Scenarios
{
    /// <summary>
    /// 对话行链式构建器
    /// </summary>
    public class DialogueLineBuilder
    {
        private readonly ADVScenarioBase scenario;
        private readonly DialogueLine line;

        public DialogueLineBuilder(ADVScenarioBase scenario, string speaker, string content) {
            this.scenario = scenario;
            line = new DialogueLine(speaker, content);
        }

        /// <summary>
        /// 设置开始事件
        /// </summary>
        public DialogueLineBuilder OnStart(Action action) {
            line.OnStart = action;
            return this;
        }

        /// <summary>
        /// 设置完成事件
        /// </summary>
        public DialogueLineBuilder OnComplete(Action action) {
            line.OnComplete = action;
            return this;
        }

        /// <summary>
        /// 设置对话框样式
        /// </summary>
        public DialogueLineBuilder WithStyle(Func<DialogueBoxBase> styleProvider) {
            line.StyleOverride = styleProvider;
            return this;
        }

        /// <summary>
        /// 使用深海风格
        /// </summary>
        public DialogueLineBuilder WithSeaStyle() {
            line.StyleOverride = () => SeaDialogueBox.Instance;
            return this;
        }

        /// <summary>
        /// 使用硫磺火风格
        /// </summary>
        public DialogueLineBuilder WithBrimstoneStyle() {
            line.StyleOverride = () => BrimstoneDialogueBox.Instance;
            return this;
        }

        /// <summary>
        /// 添加选项
        /// </summary>
        public DialogueLineBuilder WithChoices(params Choice[] choices) {
            line.Choices = [.. choices];
            return this;
        }

        /// <summary>
        /// 添加选项（使用列表）
        /// </summary>
        public DialogueLineBuilder WithChoices(List<Choice> choices) {
            line.Choices = choices;
            return this;
        }

        /// <summary>
        /// 设置选项框样式
        /// </summary>
        public DialogueLineBuilder WithChoiceBoxStyle(ADVChoiceBox.ChoiceBoxStyle style) {
            line.ChoiceBoxStyle = style;
            return this;
        }

        /// <summary>
        /// 完成构建并添加到场景
        /// </summary>
        public void Build() {
            scenario.Add(line);
        }
    }
}
