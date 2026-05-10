using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.PrimePlasamas
{
    /// <summary>
    /// 原型等离子皮下护甲
    /// <br/>循环系统槽位的被动型义体，沿全身皮下铺展电活性凝胶层
    /// <br/>装备期间持续提升玩家的<see cref="DefenseBonus"/>点防御与<see cref="EnduranceBonus"/>伤害减免，
    /// 并通过<see cref="KnockbackResistanceBonus"/>提供高强度的击退抗性
    /// <br/>所有数值修改在 <see cref="PostUpdateEquipped"/> 中写入，与原版护甲共享同一阶段，确保不会被本帧
    /// <see cref="ModPlayer.ResetEffects"/> 复位，也不会与其他义体的属性写入产生竞争
    /// </summary>
    internal class PrimePlasama : BaseCyberware
    {
        /// <summary>
        /// 被动提供的防御点数
        /// </summary>
        public const int DefenseBonus = 25;

        /// <summary>
        /// 被动提供的额外伤害减免（叠加在防御后的乘性减免上）
        /// </summary>
        public const float EnduranceBonus = 0.06f;

        /// <summary>
        /// 击退抗性（0~1），越接近1越难被击退
        /// </summary>
        public const float KnockbackResistanceBonus = 0.6f;

        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.CirculatorySystem;

        public override int CapacityCost => 3;

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(0, 6, 0, 0);
        }

        /// <summary>
        /// 查询指定玩家是否装备了 <see cref="PrimePlasama"/>，未装备返回 null
        /// </summary>
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
            //防御与伤害减免直接累加，与原版护甲计算一致
            player.statDefense += DefenseBonus;
            player.endurance += EnduranceBonus;
            //真正的击退抗性通过 PrimePlasamaPlayer.ModifyHurt 在受击瞬间缩放 modifiers.Knockback
            //此处不做处理，避免与原版的 noKnockback 行为发生冲突
        }
    }
}
