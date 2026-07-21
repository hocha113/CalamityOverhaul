using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Cyberwares.Skills;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.OmniElectricFoots
{
    /// <summary>
    /// 全向电动义足，足部槽，空中二段跳、地面蓄力跳
    /// <br/>蓄力满 60 帧，倍率 1.15~2.5x，进度见 <see cref="OmniElectricFootHUD"/>
    /// </summary>
    internal class OmniElectricFoot : BaseCyberware
    {
        /// <summary>满蓄帧数</summary>
        public const int FullChargeTicks = 60;

        /// <summary>最低蓄力跳倍率</summary>
        public const float MinChargeJumpMul = 1.15f;

        /// <summary>满蓄跳倍率</summary>
        public const float MaxChargeJumpMul = 2.5f;

        /// <summary>二段跳初速度</summary>
        public const float DoubleJumpSpeed = 7.6f;

        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.Feet;

        public override int CapacityCost => 3;

        public override CyberwareSkillBase ActiveSkill => OmniElectricFootSkill.Instance;

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.sellPrice(0, 7, 0, 0);
        }

        /// <summary>未装备返回 null</summary>
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
            //嵌入绑定键，未绑定时可读提示
            string keyHint = CWRKeySystem.CyberwareSkill_Key?.GetAssignedKeys() is { Count: > 0 } keys
                ? $"[{keys[0]}]"
                : CWRKeySystem.Notbound.Value + $"[{CWRKeySystem.CyberwareSkill_Key?.DisplayName}]";
            tooltips.Add(new TooltipLine(Mod, "CyberwareSkillHint",
                Language.GetTextValue("Mods.CalamityOverhaul.Items.OmniElectricFoot.SkillHint", keyHint)));
        }
    }
}
