using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Cyberwares.Skills;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.PlowSteelClampArms
{
    /// <summary>
    /// 犁钢钳臂，手部槽，挖掘/放置范围+磁吸
    /// <br/>技能高热单分子线，短/长双形态，见 <see cref="MonomolecularWire"/>
    /// </summary>
    internal class PlowSteelClampArm : BaseCyberware
    {
        /// <summary>挖掘/放置范围扩展 tile</summary>
        public const int TileRangeBonus = 4;

        /// <summary>磁吸拾取距离像素</summary>
        public const float ItemPickupRangePixels = 96f;

        /// <summary>单分子线技能冷却帧</summary>
        public const int SkillCooldown = 60 * 8;

        /// <summary>长线模式持续帧，锚点伤害判定窗口</summary>
        public const int WireLifetime = 60 * 5;

        /// <summary>短线持续帧，无锚点快速铺线</summary>
        public const int ShortWireLifetime = 60 * 2;

        /// <summary>短线长度像素，约 12 tile</summary>
        public const float ShortWireLengthPixels = 16f * 12f;

        /// <summary>长线最大锚点距离像素，超出则降级短线</summary>
        public const float MaxAnchorDistance = 16f * 32f;

        /// <summary>单分子线伤害周期帧</summary>
        public const int WireHitCooldown = 30;

        /// <summary>单分子线基础伤害</summary>
        public const int WireBaseDamage = 60;

        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.Hands;

        public override int CapacityCost => 3;

        public override CyberwareSkillBase ActiveSkill => PlowSteelClampArmSkill.Instance;

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.LightRed;
            Item.value = Item.sellPrice(0, 7, 0, 0);
        }

        /// <summary>未装备返回 null</summary>
        public static PlowSteelClampArm GetEquipped(Player player) {
            if (player == null || !player.active) {
                return null;
            }
            CyberwarePlayer cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
            if (cyberPlayer?.EquippedCyberwares == null) {
                return null;
            }
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                if (cyberPlayer.EquippedCyberwares[i]?.ModItem is PlowSteelClampArm arm) {
                    return arm;
                }
            }
            return null;
        }

        public override void PostUpdateEquipped(Player player) {
            //blockRange + equippedAnyTileRangeAcc 走原版范围流程
            player.blockRange += TileRangeBonus;
            player.equippedAnyTileRangeAcc = true;

            //磁吸物品与金币
            player.treasureMagnet = true;
            player.goldRing = true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            string skillHint = CWRKeySystem.CyberwareSkill_Key?.GetAssignedKeys() is { Count: > 0 } skillKeys
                ? $"[{skillKeys[0]}]"
                : CWRKeySystem.Notbound.Value + $"[{CWRKeySystem.CyberwareSkill_Key?.DisplayName}]";
            string radialHint = CWRKeySystem.RadialWheel_Key?.GetAssignedKeys() is { Count: > 0 } radialKeys
                ? $"[{radialKeys[0]}]"
                : CWRKeySystem.Notbound.Value + $"[{CWRKeySystem.RadialWheel_Key?.DisplayName}]";
            tooltips.Add(new TooltipLine(Mod, "CyberwareSkillHint",
                Language.GetTextValue("Mods.CalamityOverhaul.Items.PlowSteelClampArm.SkillHint",
                    skillHint, radialHint)));
        }
    }
}
