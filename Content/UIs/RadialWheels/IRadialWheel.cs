namespace CalamityOverhaul.Content.UIs.RadialWheels
{
    /// <summary>
    /// 快捷转盘契约；开关键与排布均由 <see cref="RadialWheelHub"/> 统一掌管
    /// <br/>多个转盘可同时展开，竖向避让排布，光标同一时刻只归属其中一个
    /// </summary>
    internal interface IRadialWheel
    {
        /// <summary>转盘标识，仅用于调试与日志</summary>
        string WheelId { get; }

        /// <summary>本帧是否处于展开状态</summary>
        bool WheelIsOpen { get; }

        /// <summary>本帧是否够格展开，Hub 按此决定按键要开哪几个盘</summary>
        bool WheelCanOpen { get; }

        /// <summary>堆叠次序，越小越靠屏幕下方；同号按注册先后</summary>
        int WheelStackOrder { get; }

        /// <summary>占位半径（含装饰环），Hub 据此竖向避让</summary>
        float WheelFootprintRadius { get; }

        /// <summary>Hub 开盘；silent 时不播开盘音，避免多盘齐开时音效叠一起</summary>
        void WheelOpen(bool silent);

        /// <summary>Hub 收盘；silent 时不播关盘音</summary>
        void WheelClose(bool silent);

        /// <summary>松键提交：仅焦点盘会收到，选定当前悬停项</summary>
        void WheelCommitHovered();

        /// <summary>Hub 分配的屏幕中心（UI 空间）</summary>
        void WheelSetCenter(Vector2 center);
    }
}
