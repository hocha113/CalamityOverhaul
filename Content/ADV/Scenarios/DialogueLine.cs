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
        /// <summary>
        /// 说话者名称(显示在对话框中)
        /// </summary>
        public string Speaker { get; set; }

        /// <summary>
        /// 立绘键(用于查找头像纹理，如果为null则使用Speaker)
        /// 允许角色名和立绘显示分离，解决同一角色多表情的问题
        /// </summary>
        public string PortraitKey { get; set; }

        public string Content { get; set; }
        public Action OnStart { get; set; }
        public Action OnComplete { get; set; }
        public Func<DialogueBoxBase> StyleOverride { get; set; }
        public List<Choice> Choices { get; set; }
        public ADVChoiceBox.ChoiceBoxStyle ChoiceBoxStyle { get; set; } = ADVChoiceBox.ChoiceBoxStyle.Default;

        /// <summary>
        /// 定时对话配置，如果为null则为普通对话（无时间限制）
        /// </summary>
        public TimedDialogueConfig TimedConfig { get; set; }

        /// <summary>
        /// 是否为定时对话
        /// </summary>
        public bool IsTimed => TimedConfig != null;

        public DialogueLine(string speaker, string content) {
            Speaker = speaker;
            Content = content;
            PortraitKey = null;
        }

        /// <summary>
        /// 创建对话行(角色名和立绘分离)
        /// </summary>
        /// <param name="speaker">显示的说话者名称</param>
        /// <param name="portraitKey">用于查找立绘的键</param>
        /// <param name="content">对话内容</param>
        public DialogueLine(string speaker, string portraitKey, string content) {
            Speaker = speaker;
            PortraitKey = portraitKey;
            Content = content;
        }
    }
}
