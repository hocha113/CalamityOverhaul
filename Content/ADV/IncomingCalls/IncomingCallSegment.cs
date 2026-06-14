using System;

namespace CalamityOverhaul.Content.ADV.IncomingCalls
{
    /// <summary>来电台词段数据</summary>
    public class IncomingCallSegment
    {
        /// <summary>说话者名称</summary>
        public string Speaker;

        /// <summary>台词内容</summary>
        public string Content;

        /// <summary>立绘键（为null时使用Speaker）</summary>
        public string PortraitKey;

        /// <summary>本段台词开始时回调</summary>
        public Action OnStart;

        /// <summary>本段台词结束时回调</summary>
        public Action OnFinish;

        /// <summary>自动推进延迟帧，0 须手动点击，>0 打字后等待 N 帧</summary>
        public int AutoAdvanceDelay;
    }
}
