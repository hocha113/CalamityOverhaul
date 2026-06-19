using System;

namespace CalamityOverhaul.Content.ADV.DialogueBoxs
{
    /// <summary>对话段数据</summary>
    public class DialogueSegment
    {
        /// <summary>说话者名称（用于显示）</summary>
        public string Speaker;

        /// <summary>对话内容</summary>
        public string Content;

        /// <summary>对话开始时的回调</summary>
        public Action OnStart;

        /// <summary>对话结束时的回调</summary>
        public Action OnFinish;

        /// <summary>立绘键，null 时用 Speaker，角色名与立绘可分离</summary>
        public string PortraitKey;

        /// <summary>定时配置，null 为普通对话</summary>
        public TimedDialogueConfig TimedConfig;

        /// <summary>是否为定时对话</summary>
        public bool IsTimed => TimedConfig != null;

        /// <summary>是否为特殊节点（含选项/用户回调/定时等事件），跳过对话时会停在此处而非略过</summary>
        public bool IsSpecial;
    }
}
