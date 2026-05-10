using CalamityOverhaul.Common;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.OmniElectricFoots
{
    /// <summary>
    /// 全向电动义足 —— "OmniElectric Foot"
    /// <br/>足部槽位的高强度运动义体，结合电磁反冲与高密度推进凝胶
    /// <list type="bullet">
    ///   <item>装备后获得一次<b>空中二段跳</b>，落地或起跳即重置使用次数</item>
    ///   <item>按住 <see cref="CWRKeySystem.CyberwareSkill_Key"/> 在地面蓄力，松开后释放<b>蓄力跳</b>
    ///         蓄满状态下提供约 2.5 倍于普通跳跃的初速度</item>
    ///   <item>蓄力进度通过 <see cref="OmniElectricFootHUD"/> 实时显示在玩家身侧</item>
    /// </list>
    /// 全部运行时状态由 <see cref="OmniElectricFootPlayer"/> 维护，本物品类只承担属性元数据
    /// </summary>
    internal class OmniElectricFoot : BaseCyberware
    {
        /// <summary>
        /// 完整蓄满所需帧数
        /// </summary>
        public const int FullChargeTicks = 60;

        /// <summary>
        /// 蓄力跳跃在最低蓄力时的速度倍率（线性插值的下限）
        /// </summary>
        public const float MinChargeJumpMul = 1.15f;

        /// <summary>
        /// 蓄力跳跃在完全蓄满时的速度倍率（线性插值的上限）
        /// </summary>
        public const float MaxChargeJumpMul = 2.5f;

        /// <summary>
        /// 二段跳的初速度（绝对值，向上为正）
        /// </summary>
        public const float DoubleJumpSpeed = 7.6f;

        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.Feet;

        public override int CapacityCost => 3;

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.sellPrice(0, 7, 0, 0);
        }

        /// <summary>
        /// 查询指定玩家是否装备了 <see cref="OmniElectricFoot"/>，未装备返回 null
        /// </summary>
        public static OmniElectricFoot GetEquipped(Player player) {
            if (player == null || !player.active) {
                return null;
            }
            CyberwarePlayer cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
            if (cyberPlayer?.EquippedCyberwares == null) {
                return null;
            }
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                if (cyberPlayer.EquippedCyberwares[i]?.ModItem is OmniElectricFoot foot) {
                    return foot;
                }
            }
            return null;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            //在工具提示中嵌入实际绑定的快捷键，未绑定时给出可读的"未绑定"提示
            string keyHint = CWRKeySystem.CyberwareSkill_Key?.GetAssignedKeys() is { Count: > 0 } keys
                ? $"[{keys[0]}]"
                : CWRLocText.Instance.Notbound.Value + $"[{CWRKeySystem.CyberwareSkill_Key?.DisplayName}]";
            tooltips.Add(new TooltipLine(Mod, "CyberwareSkillHint",
                Language.GetTextValue("Mods.CalamityOverhaul.Items.OmniElectricFoot.SkillHint", keyHint)));
        }
    }
}
