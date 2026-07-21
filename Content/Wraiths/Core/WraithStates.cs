namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>实体存在态，与绑定无关；权威推进，客户端 SyncVar 跟随</summary>
    public enum WraithPresence : byte
    {
        /// <summary>蛰伏，保留值</summary>
        Dormant,
        /// <summary>显形过渡</summary>
        Materializing,
        /// <summary>完全显形</summary>
        Present,
        /// <summary>消散过渡，结束后销毁</summary>
        Dematerializing,
        /// <summary>死机窗，可持载体行仪式；窗尽未消耗则消散</summary>
        Halted,
    }

    /// <summary>载体绑定态；躁动由驾驭度推导，不单开状态</summary>
    public enum WraithBindState : byte
    {
        /// <summary>从未遭遇</summary>
        Unknown,
        /// <summary>遭遇过未铭刻</summary>
        Discovered,
        /// <summary>已铭刻</summary>
        Bound,
        /// <summary>封印不可用</summary>
        Sealed,
    }
}
