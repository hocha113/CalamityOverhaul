namespace CalamityOverhaul.Content.UIs.HudStack
{
    /// <summary>
    /// 左下角 HUD 队列成员契约。任意想要占据屏幕左下角的常驻 HUD 实现本接口，
    /// 并在自身定位（绘制与命中盒）时统一改用 <see cref="BottomLeftHudStack.ResolveAnchor"/> 取锚点，
    /// 即可交由 <see cref="BottomLeftHudStack"/> 统一编排竖直堆叠、自动避免互相遮挡。
    /// <br/>独占显示时位置保持不变；当多个成员同时显示，次序更大的成员会被平滑顶高悬浮。
    /// </summary>
    internal interface IBottomLeftHud
    {
        /// <summary>
        /// 本帧是否需要占用一个堆叠槽位，通常直接返回该 HUD 现有的可见判定。
        /// 仅当为 <see langword="true"/> 时参与排布，并将上方成员顶高。
        /// </summary>
        bool HudStackActive { get; }

        /// <summary>
        /// 堆叠次序，越小越靠近屏幕左下角（底部）。
        /// 底部成员保持自然位置，次序更大的成员依次向上堆叠；同序号按注册先后稳定排序。
        /// </summary>
        int HudStackOrder { get; }

        /// <summary>
        /// 该 HUD 的自然锚点（未发生避让时的原始位置）。
        /// 框架只在此基础上叠加一个竖直避让偏移，因此独占时外观与原先一致。
        /// <br/>可能与其它成员同时堆叠的成员，应使用彼此一致的坐标系（推荐 UI 空间，见 <see cref="BottomLeftHudStack.UIScreenH"/>）。
        /// </summary>
        Vector2 HudStackAnchor { get; }

        /// <summary>
        /// 自锚点向上的占用高度（像素），供上方成员计算需要让出的空间。
        /// </summary>
        float HudStackTopExtent { get; }

        /// <summary>
        /// 自锚点向下的占用高度（像素），决定本成员被顶高后底边停靠的位置。
        /// </summary>
        float HudStackBottomExtent { get; }
    }
}
