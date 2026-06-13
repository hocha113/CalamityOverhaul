namespace CalamityOverhaul.Content.ADV.DialogueBoxs
{
    /// <summary>
    /// 对话框生命周期状态
    /// </summary>
    public enum DialogueBoxState
    {
        /// <summary>
        /// 空闲
        /// </summary>
        Idle,
        /// <summary>
        /// 打开动画中
        /// </summary>
        Opening,
        /// <summary>
        /// 激活，显示对话
        /// </summary>
        Active,
        /// <summary>
        /// 暂停，仍显示
        /// </summary>
        Paused,
        /// <summary>
        /// 关闭动画中
        /// </summary>
        Closing,
        /// <summary>
        /// 已关闭
        /// </summary>
        Closed
    }
}
