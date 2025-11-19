using System;

namespace CalamityOverhaul.Content.ADV.ADVChoices
{
    /// <summary>
    /// 选项数据类
    /// </summary>
    public class Choice
    {
        public string Text { get; set; }
        public Action OnSelect { get; set; }
        public bool Enabled { get; set; } = true;
        public string DisabledHint { get; set; }

        public Choice(string text, Action onSelect, bool enabled = true, string disabledHint = null) {
            Text = text;
            OnSelect = onSelect;
            Enabled = enabled;
            DisabledHint = disabledHint;
        }
    }

    /// <summary>
    /// 悬停状态变化事件参数
    /// </summary>
    public class ChoiceHoverEventArgs : EventArgs
    {
        /// <summary>
        /// 当前悬停的选项索引（-1表示无悬停）
        /// </summary>
        public int CurrentIndex { get; }

        /// <summary>
        /// 之前悬停的选项索引（-1表示无悬停）
        /// </summary>
        public int PreviousIndex { get; }

        /// <summary>
        /// 当前悬停的选项对象（如果有）
        /// </summary>
        public Choice CurrentChoice { get; }

        /// <summary>
        /// 之前悬停的选项对象（如果有）
        /// </summary>
        public Choice PreviousChoice { get; }

        public ChoiceHoverEventArgs(int currentIndex, int previousIndex, Choice currentChoice, Choice previousChoice) {
            CurrentIndex = currentIndex;
            PreviousIndex = previousIndex;
            CurrentChoice = currentChoice;
            PreviousChoice = previousChoice;
        }
    }
}
