using CalamityOverhaul.Content.RAMSystems;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.CstmVisualEyes
{
    /// <summary>CSTM 视像义眼 RAM 修饰器</summary>
    internal sealed class CstmVisualEyeRamProvider : IRamModifierProvider, ICWRLoader
    {
        public int MaxRamBonus => CstmVisualEye.RamCapacityBonus;
        public float RecoveryRateBonus => 0f;
        public bool IsActive(Player player) => CstmVisualEye.GetEquipped(player) != null;

        void ICWRLoader.LoadData() => RamSystem.RegisterProvider(this);
        void ICWRLoader.UnLoadData() => RamSystem.UnregisterProvider(this);
    }
}
