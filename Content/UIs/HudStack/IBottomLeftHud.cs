namespace CalamityOverhaul.Content.UIs.HudStack
{
    /// <summary>
    /// 左下角 HUD 堆叠契约；定位改用 <see cref="BottomLeftHudStack.ResolveAnchor"/>
    /// <br/>多成员同显时次序更大者被顶高；独占时位置不变
    /// </summary>
    internal interface IBottomLeftHud
    {
        /// <summary>本帧是否占堆叠槽，通常即可见判定</summary>
        bool HudStackActive { get; }

        /// <summary>堆叠次序，越小越靠底；同号按注册先后</summary>
        int HudStackOrder { get; }

        /// <summary>自然锚点；坐标系建议 UI 空间，见 <see cref="BottomLeftHudStack.UIScreenH"/></summary>
        Vector2 HudStackAnchor { get; }

        /// <summary>自锚点向上占用高(px)</summary>
        float HudStackTopExtent { get; }

        /// <summary>自锚点向下占用高(px)</summary>
        float HudStackBottomExtent { get; }
    }
}
