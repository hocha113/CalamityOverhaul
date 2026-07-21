using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.PrimePlasamas
{
    /// <summary>
    /// 原型等离子皮下护甲，循环系统槽
    /// <br/>+DefenseBonus/+EnduranceBonus，击退经 PrimePlasamaPlayer.ModifyHurt 缩放
    /// <br/>数值 PostUpdateEquipped 写入，与 ResetEffects 不冲突
    /// </summary>
    internal class PrimePlasama : BaseCyberware
    {
        /// <summary>防御加成</summary>
        public const int DefenseBonus = 25;

        /// <summary>伤害减免加成</summary>
        public const float EnduranceBonus = 0.06f;

        /// <summary>击退抗性 0~1</summary>
        public const float KnockbackResistanceBonus = 0.6f;

        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.CirculatorySystem;

        public override int CapacityCost => 3;

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(0, 6, 0, 0);
        }

        /// <summary>未装备返回 null</summary>
        public static PrimePlasama GetEquipped(Player player) {
            if (player == null || !player.active) {
                return null;
            }
            CyberwarePlayer cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
            if (cyberPlayer?.EquippedCyberwares == null) {
                return null;
            }
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                if (cyberPlayer.EquippedCyberwares[i]?.ModItem is PrimePlasama plasama) {
                    return plasama;
                }
            }
            return null;
        }

        public override void PostUpdateEquipped(Player player) {
            player.statDefense += DefenseBonus;
            player.endurance += EnduranceBonus;
            //击退经 ModifyHurt 缩放 modifiers.Knockback，此处不写 noKnockback
        }
    }
}
