namespace CalamityOverhaul.Content.RAMSystems
{
    /// <summary>运行时 RAM 修饰器，IsActive 为 false 时跳过聚合</summary>
    public interface IRamModifierProvider
    {
        /// <summary>RAM 上限额外格数</summary>
        int MaxRamBonus { get; }
        /// <summary>恢复速度额外量（每秒）</summary>
        float RecoveryRateBonus { get; }
        /// <summary>当前是否生效</summary>
        bool IsActive { get; }
    }
}
