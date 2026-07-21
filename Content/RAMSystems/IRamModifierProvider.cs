namespace CalamityOverhaul.Content.RAMSystems
{
    /// <summary>运行时修饰器，!IsActive 跳过</summary>
    public interface IRamModifierProvider
    {
        int MaxRamBonus { get; }
        /// <summary>恢复额外量/秒</summary>
        float RecoveryRateBonus { get; }
        bool IsActive { get; }
    }
}
