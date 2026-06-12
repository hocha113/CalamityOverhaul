namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    /// <summary>头部向四臂广播的战术指令，写入头部 <c>npc.ai[1]</c></summary>
    internal enum PrimeCommandKind : int
    {
        None = 0,
        /// <summary>物理突击：钳爪突刺 + 电锯冲锋</summary>
        PhysicalAssault = 1,
        /// <summary>火力压制：激光横扫 + 火箭迫击</summary>
        FireSuppression = 2,
        /// <summary>十字绞杀：四臂合体封位</summary>
        CrossExecute = 3,
    }
}
