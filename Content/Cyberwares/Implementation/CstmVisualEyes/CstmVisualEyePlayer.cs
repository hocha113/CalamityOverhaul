using CalamityOverhaul.Content.RAMSystems;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.CstmVisualEyes
{
    /// <summary>
    /// CSTM 视像义眼对应的 RAM 修饰器
    /// <br/>以单例方式挂入本机玩家的 RAM 系统，每帧聚合时通过 <see cref="IsActive"/> 自查装备状态
    /// <br/>装备本义眼期间额外提供 <see cref="CstmVisualEye.RamCapacityBonus"/> 的 RAM 上限加成
    /// </summary>
    internal sealed class CstmVisualEyeRamProvider : IRamModifierProvider
    {
        public int MaxRamBonus => CstmVisualEye.RamCapacityBonus;
        public float RecoveryRateBonus => 0f;
        public bool IsActive => CstmVisualEye.GetEquipped(Main.LocalPlayer) != null;
    }

    /// <summary>
    /// CSTM 视像义眼的玩家组件
    /// <br/>仅负责在玩家进入世界时一次性向 <see cref="RamSystem"/> 注册 <see cref="CstmVisualEyeRamProvider"/>
    /// <br/>提供器内部以装备查询自动开关贡献，无需关心装备/卸载事件，亦不会因 Item 克隆产生实例膨胀
    /// </summary>
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
