using CalamityOverhaul.Content.HackTimes;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.CstmVisualEyes
{
    /// <summary>
    /// CSTM 视像义眼，光学槽位网络义体
    /// <br/>HackTimeAccess 谓词授权骇客时间；+RamCapacityBonus RAM 上限；左下 RAM 弧条 HUD
    /// <br/>手持 SHPC 时 SHPCUI 接管；RAM 提供器 OnEnterWorld 挂入，IsActive 自查装备
    /// </summary>
    internal class CstmVisualEye : BaseCyberware
    {
        /// <summary>RAM 上限加成，装备期间生效</summary>
        public const int RamCapacityBonus = 4;

        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.OcularSystem;

        public override int CapacityCost => 4;

        /// <summary>
        /// SetStaticDefaults 注册 HackTime 谓词，动态查装备，避免 Item.Clone 实例错位
        /// </summary>
        public override void SetStaticDefaults() {
            HackTimeAccess.Register(player => GetEquipped(player) != null, "Cyberware:CstmVisualEye");
        }

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.Cyan;
            Item.value = Item.sellPrice(0, 12, 0, 0);
        }

        /// <summary>查询玩家是否装备本义体，未装备返回 null</summary>
        public static CstmVisualEye GetEquipped(Player player) {
            if (player == null || !player.active) {
                return null;
            }
            CyberwarePlayer cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
            if (cyberPlayer?.EquippedCyberwares == null) {
                return null;
            }
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                if (cyberPlayer.EquippedCyberwares[i]?.ModItem is CstmVisualEye eye) {
                    return eye;
                }
            }
            return null;
        }
    }
}
