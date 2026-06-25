namespace CalamityOverhaul.Content.UIs.HudStack
{
    /// <summary>
    /// 左下角 HUD 队列成员契约
    /// <br/>实现本接口并在定位（绘制与命中盒）时改用 <see cref="BottomLeftHudStack.ResolveAnchor"/> 取锚点，
    /// 即可交由 <see cref="BottomLeftHudStack"/> 统一编排竖直堆叠、避免互相遮挡
    /// <br/>独占时位置不变，多成员同显时次序更大者被平滑顶高悬浮
    /// </summary>
    internal interface IBottomLeftHud
    {
        /// <summary>
        /// 本帧是否需要占用堆叠槽位，通常直接返回该 HUD 现有可见判定
        /// <br/>为 <see langword="true"/> 时参与排布并把上方成员顶高
        /// </summary>
        bool HudStackActive { get; }

        /// <summary>
        /// 堆叠次序，越小越靠近屏幕左下角（底部）
        /// <br/>底部成员保持自然位置，更大者依次向上堆叠；同序号按注册先后稳定排序
        /// </summary>
        int HudStackOrder { get; }

        /// <summary>
        /// 该 HUD 的自然锚点（未避让时的原始位置）
        /// <br/>框架仅在此叠加竖直避让偏移，故独占时外观与原先一致
        /// <br/>可能与他人同堆叠者应使用一致坐标系（推荐 UI 空间，见 <see cref="BottomLeftHudStack.UIScreenH"/>）
        /// </summary>
        Vector2 HudStackAnchor { get; }

        /// <summary>自锚点向上的占用高度（像素），供上方成员计算需让出的空间</summary>
        float HudStackTopExtent { get; }

        /// <summary>自锚点向下的占用高度（像素），决定被顶高后底边停靠的位置</summary>
        float HudStackBottomExtent { get; }
    }
}
