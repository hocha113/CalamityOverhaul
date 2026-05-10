using CalamityOverhaul.Common;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.SelfHackCrystals
{
    /// <summary>
    /// 自骇水晶 —— "Self-Hack Crystal"
    /// <br/>操作系统槽位的高阶网络义体，能将骇客接口反向指向植入者本人
    /// <list type="bullet">
    ///   <item>装备期间持续提供 <see cref="RamRecoveryBonus"/> 点 RAM/秒 的额外恢复速度</item>
    ///   <item>义体技能：消耗 <see cref="SkillRamCost"/> 点 RAM 立即清除全部 debuff，
    ///         并附带 <see cref="ImmunityFrames"/> 帧的无敌时间，给玩家窗口跳脱致命情况</item>
    /// </list>
    /// 玩家行为收束在 <see cref="SelfHackCrystalPlayer"/>；与 <see cref="CstmVisualEyes.CstmVisualEye"/>
    /// 相同的"在世界进入时一次性挂入 RAM 提供器"模式，自我查询装备状态决定是否生效
    /// </summary>
    internal class SelfHackCrystal : BaseCyberware
    {
        /// <summary>
        /// 提供给 RAM 系统的额外每秒恢复量（与原版基础 0.1/s 同量纲）
        /// </summary>
        public const float RamRecoveryBonus = 0.15f;

        /// <summary>
        /// 释放自骇技能时消耗的 RAM 点数
        /// </summary>
        public const int SkillRamCost = 3;

        /// <summary>
        /// 自骇成功后的无敌帧
        /// </summary>
        public const int ImmunityFrames = 90;

        /// <summary>
        /// 自骇技能冷却帧
        /// </summary>
        public const int SkillCooldown = 60 * 12;

        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.OperatingSystem;

        public override int CapacityCost => 4;

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.Cyan;
            Item.value = Item.sellPrice(0, 9, 0, 0);
        }

        /// <summary>
        /// 查询指定玩家是否装备了 <see cref="SelfHackCrystal"/>，未装备返回 null
        /// </summary>
        public static SelfHackCrystal GetEquipped(Player player) {
            if (player == null || !player.active) {
                return null;
            }
            CyberwarePlayer cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
            if (cyberPlayer?.EquippedCyberwares == null) {
                return null;
            }
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                if (cyberPlayer.EquippedCyberwares[i]?.ModItem is SelfHackCrystal sh) {
                    return sh;
                }
            }
            return null;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            string keyHint = CWRKeySystem.CyberwareSkill_Key?.GetAssignedKeys() is { Count: > 0 } keys
                ? $"[{keys[0]}]"
                : CWRLocText.Instance.Notbound.Value + $"[{CWRKeySystem.CyberwareSkill_Key?.DisplayName}]";
            tooltips.Add(new TooltipLine(Mod, "CyberwareSkillHint",
                Language.GetTextValue("Mods.CalamityOverhaul.Items.SelfHackCrystal.SkillHint", keyHint)));
        }
    }
}
