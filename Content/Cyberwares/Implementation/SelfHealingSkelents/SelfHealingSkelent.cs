using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.SelfHealingSkelents
{
    /// <summary>
    /// 自愈骨骼，骨骼槽被动
    /// <br/>常驻 +LifeRegenBonus，脱战 OutOfCombatThreshold 后纳米修复
    /// <br/>无坠落伤，+MaxLifeBonus；lifeRegen 须写 UpdateLifeRegen
    /// </summary>
    internal class SelfHealingSkelent : BaseCyberware
    {
        /// <summary>常驻生命回复，4≈2HP/秒</summary>
        public const int LifeRegenBonus = 4;

        /// <summary>脱战阈值帧，约 4 秒</summary>
        public const int OutOfCombatThreshold = 60 * 4;

        /// <summary>脱战后追加回复，叠加 LifeRegenBonus</summary>
        public const int OutOfCombatRegenBonus = 8;

        /// <summary>最大生命加成</summary>
        public const int MaxLifeBonus = 40;

        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.Skeleton;

        public override int CapacityCost => 3;

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(0, 6, 0, 0);
        }

        /// <summary>未装备返回 null</summary>
        public static SelfHealingSkelent GetEquipped(Player player) {
            if (player == null || !player.active) {
                return null;
            }
            CyberwarePlayer cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
            if (cyberPlayer?.EquippedCyberwares == null) {
                return null;
            }
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                if (cyberPlayer.EquippedCyberwares[i]?.ModItem is SelfHealingSkelent sk) {
                    return sk;
                }
            }
            return null;
        }

        public override void PostUpdateEquipped(Player player) {
            //装备即无坠落伤，防高摔窗口被吞
            player.noFallDmg = true;
        }
    }
}
