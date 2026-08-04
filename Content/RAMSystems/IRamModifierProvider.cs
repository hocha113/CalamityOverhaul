namespace CalamityOverhaul.Content.RAMSystems
{
    /// <summary>按玩家计算的 RAM 修饰器</summary>
    public interface IRamModifierProvider
    {
        int MaxRamBonus { get; }
        /// <summary>恢复额外量/秒</summary>
        float RecoveryRateBonus { get; }
        bool IsActive(Terraria.Player player);
    }
}
