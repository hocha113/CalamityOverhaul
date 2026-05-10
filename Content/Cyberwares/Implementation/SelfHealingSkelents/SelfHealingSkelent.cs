using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.SelfHealingSkelents
{
    /// <summary>
    /// 自愈骨骼 —— "Self-Healing Skelent"
    /// <br/>骨骼槽位的高阶生体义体，用纳米晶格替换原生骨架
    /// <list type="bullet">
    ///   <item>持续提供 <see cref="LifeRegenBonus"/> 点常驻生命回复</item>
    ///   <item>脱离战斗 <see cref="OutOfCombatThreshold"/> 帧后进入<b>纳米修复</b>状态，
    ///         追加 <see cref="OutOfCombatRegenBonus"/> 点回复并大幅缩短回复延迟</item>
    ///   <item>完全免疫坠落伤害；金属义骨结构同时提升 <see cref="MaxLifeBonus"/> 点最大生命</item>
    /// </list>
    /// 全部行为是被动式，不抢占任何按键；状态记录在 <see cref="SelfHealingSkelentPlayer"/> 中
    /// </summary>
    internal class SelfHealingSkelent : BaseCyberware
    {
        /// <summary>
        /// 常驻生命回复加成（原版每 0.5HP/60 帧为单位，4 ≈ 2HP/秒）
        /// </summary>
        public const int LifeRegenBonus = 4;

        /// <summary>
        /// 脱战阈值（帧）。约 4 秒后进入纳米修复状态
        /// </summary>
        public const int OutOfCombatThreshold = 60 * 4;

        /// <summary>
        /// 脱战后追加的回复值（叠加在 <see cref="LifeRegenBonus"/> 之上）
        /// </summary>
        public const int OutOfCombatRegenBonus = 8;

        /// <summary>
        /// 直接附加在玩家最大生命上的硬性加成
        /// </summary>
        public const int MaxLifeBonus = 40;

        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.Skeleton;

        public override int CapacityCost => 3;

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(0, 6, 0, 0);
        }

        /// <summary>
        /// 查询指定玩家是否装备了 <see cref="SelfHealingSkelent"/>，未装备返回 null
        /// </summary>
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
            //坠落伤害免疫无需依赖玩家状态，只要装备就启用，避免高摔时的窗口期被吞掉
            player.noFallDmg = true;
        }
    }
}
