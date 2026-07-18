namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>
    /// 厉鬼实体的存在状态，描述一次显形（投影）的生命周期，与绑定维度<see cref="WraithBindState"/>无关。
    /// 由权威端推进，客户端经 SyncVar 跟随
    /// </summary>
    public enum WraithPresence : byte
    {
        /// <summary>蛰伏：无实体（保留值，实体存活期间不使用）</summary>
        Dormant,
        /// <summary>显形中：自无到有的过渡</summary>
        Materializing,
        /// <summary>在场：完全显形</summary>
        Present,
        /// <summary>消散中：过渡结束后实体销毁</summary>
        Dematerializing,
    }

    /// <summary>
    /// 厉鬼与载体（刀/玩家）的绑定状态，结构维度。
    /// "躁动"不是独立状态：由驾驭度与规则推导，避免与 Mastery 双写矛盾
    /// </summary>
    public enum WraithBindState : byte
    {
        /// <summary>未知：从未遭遇</summary>
        Unknown,
        /// <summary>已发现：遭遇过但未铭刻</summary>
        Discovered,
        /// <summary>已铭刻：被载体驾驭</summary>
        Bound,
        /// <summary>封印中：存在但不可用</summary>
        Sealed,
    }
}
