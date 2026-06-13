namespace CalamityOverhaul.Content.ADV.IncomingCalls
{
    /// <summary>
    /// 来电系统生命周期状态
    /// </summary>
    public enum IncomingCallState
    {
        /// <summary>
        /// 空闲，未激活
        /// </summary>
        Idle,
        /// <summary>
        /// 来电滑入，振铃中
        /// </summary>
        Ringing,
        /// <summary>
        /// 接听过渡，面板展开
        /// </summary>
        Connecting,
        /// <summary>
        /// 通话中，逐条播台词
        /// </summary>
        Speaking,
        /// <summary>
        /// 挂断，面板滑出
        /// </summary>
        Ending
    }
}
