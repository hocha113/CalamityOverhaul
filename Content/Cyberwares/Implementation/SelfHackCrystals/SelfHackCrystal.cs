using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Cyberwares.Skills;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.SelfHackCrystals
{
    /// <summary>
    /// 自骇水晶，操作系统槽位
    /// <br/>常驻 +RamRecoveryBonus/s；技能耗 SkillRamCost RAM 清 debuff + ImmunityFrames 无敌
    /// <br/>RAM 提供器 OnEnterWorld 挂入，IsActive 自查装备，同 CstmVisualEye 模式
    /// </summary>
    internal class SelfHackCrystal : BaseCyberware
    {
        /// <summary>RAM 额外恢复/s，与原版 0.1/s 同量纲</summary>
        public const float RamRecoveryBonus = 0.15f;

        /// <summary>技能 RAM 消耗</summary>
        public const int SkillRamCost = 3;

        /// <summary>自骇后无敌帧</summary>
        public const int ImmunityFrames = 90;

        /// <summary>技能冷却帧</summary>
        public const int SkillCooldown = 60 * 12;

        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.OperatingSystem;

        public override int CapacityCost => 4;

        public override CyberwareSkillBase ActiveSkill => SelfHackCrystalSkill.Instance;

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.Cyan;
            Item.value = Item.sellPrice(0, 9, 0, 0);
        }

        /// <summary>查询玩家是否装备本义体，未装备返回 null</summary>
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
                : CWRKeySystem.Notbound.Value + $"[{CWRKeySystem.CyberwareSkill_Key?.DisplayName}]";
            tooltips.Add(new TooltipLine(Mod, "CyberwareSkillHint",
                Language.GetTextValue("Mods.CalamityOverhaul.Items.SelfHackCrystal.SkillHint", keyHint)));
        }
    }
}
