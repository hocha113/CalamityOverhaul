namespace CalamityOverhaul.Content.ADV.DialogueBoxs
{
    /// <summary>
    /// 对话框生命周期状态
    /// </summary>
    public enum DialogueBoxState
    {
        /// <summary>
        /// 空闲状态，对话框未激活
        /// </summary>
        Idle,
        /// <summary>
        /// 正在打开，播放打开动画中
        /// </summary>
        Opening,
        /// <summary>
        /// 激活状态，正在显示对话
        /// </summary>
        Active,
        /// <summary>
        /// 暂停状态，对话暂停但仍然显示
        /// </summary>
        Paused,
        /// <summary>
        /// 正在关闭，播放关闭动画中
        /// </summary>
        Closing,
        /// <summary>
        /// 已关闭，完成关闭动画后的状态
        /// </summary>
        Closed
    }
}
