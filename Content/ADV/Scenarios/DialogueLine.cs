using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.ADV.Scenarios
{
    /// <summary>
    /// 单条对话数据，支持绑定事件和自定义样式
    /// </summary>
    public class DialogueLine
    {
        public string Speaker { get; set; }
        public string Content { get; set; }
        public Action OnStart { get; set; }
        public Action OnComplete { get; set; }
        public Func<DialogueBoxBase> StyleOverride { get; set; }
        public List<Choice> Choices { get; set; }//选项列表
        public ADVChoiceBox.ChoiceBoxStyle ChoiceBoxStyle { get; set; } = ADVChoiceBox.ChoiceBoxStyle.Default;//选项框样式

        public DialogueLine(string speaker, string content) {
            Speaker = speaker;
            Content = content;
        }
    }
}
