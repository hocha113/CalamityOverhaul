namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    /// <summary>头部向四臂广播的战术指令，写入头部 npc.ai[1]</summary>
    internal enum PrimeCommandKind : int
    {
        None = 0,
        /// <summary>物理突击</summary>
        PhysicalAssault = 1,
        /// <summary>火力压制</summary>
        FireSuppression = 2,
        /// <summary>十字绞杀</summary>
        CrossExecute = 3,
    }
}
