using CalamityOverhaul.Content.RAMSystems;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.CstmVisualEyes
{
    /// <summary>CSTM 视像义眼 RAM 修饰器，IsActive 自查装备</summary>
    internal sealed class CstmVisualEyeRamProvider : IRamModifierProvider
    {
        public int MaxRamBonus => CstmVisualEye.RamCapacityBonus;
        public float RecoveryRateBonus => 0f;
        public bool IsActive => CstmVisualEye.GetEquipped(Main.LocalPlayer) != null;
    }

    /// <summary>CSTM 视像义眼 ModPlayer，OnEnterWorld 注册 RamProvider</summary>
    internal class CstmVisualEyePlayer : ModPlayer
    {
        private CstmVisualEyeRamProvider provider;

        public override void OnEnterWorld() {
            //仅本机玩家需要把贡献项写入本机 RAM 列表，多人模式下其他玩家的实例不必参与本机聚合
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            provider ??= new CstmVisualEyeRamProvider();
            RamSystem.RegisterProvider(new CstmVisualEyeRamProvider());
        }
    }
}
