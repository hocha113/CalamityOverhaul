using CalamityOverhaul.Common;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.PlowSteelClampArms
{
    /// <summary>
    /// 犁钢钳臂 —— "PlowSteel Clamp Arm"
    /// <br/>手部槽位的工业级义体，附带磁感拾取与高刚性钳臂
    /// <list type="bullet">
    ///   <item>装备后大幅扩展<b>挖掘 / 放置范围</b>，并永久启用<b>金钱与物品的磁吸拾取</b></item>
    ///   <item>义体技能：从掌心释放一根<b>高热单分子线</b>，把<see cref="MonomolecularWire.MaxLifetime"/>帧内
    ///         玩家与目标物块之间的空间钉成一条灼热细线，触碰者持续受灼烧伤害</item>
    /// </list>
    /// 范围加成在 <see cref="PostUpdateEquipped"/> 中实时写入，每帧都会刷新，与磁吸拾取一同生效。
    /// 技能触发逻辑由 <see cref="PlowSteelClampArmPlayer"/> 集中管理，保证只在本机玩家上执行
    /// </summary>
    internal class PlowSteelClampArm : BaseCyberware
    {
        /// <summary>
        /// 挖掘 / 放置范围扩展（单位：tile）
        /// </summary>
        public const int TileRangeBonus = 4;

        /// <summary>
        /// 拾取距离倍率（基于原版的 <c>Player.GetItemPickupRange</c> 默认范围 30 像素）
        /// </summary>
        public const float ItemPickupRangePixels = 96f;

        /// <summary>
        /// 单分子线技能冷却（帧）
        /// </summary>
        public const int SkillCooldown = 60 * 8;

        /// <summary>
        /// 单分子线持续帧数（伤害判定窗口）
        /// </summary>
        public const int WireLifetime = 60 * 5;

        /// <summary>
        /// 单分子线允许的最大触发距离（像素），超出范围视为无效目标
        /// </summary>
        public const float MaxAnchorDistance = 16f * 32f;

        /// <summary>
        /// 单分子线每次伤害周期（帧），过短会让伤害堆叠失衡
        /// </summary>
        public const int WireHitCooldown = 30;

        /// <summary>
        /// 单分子线基础伤害（叠加于受击者本帧的灼烧效果）
        /// </summary>
        public const int WireBaseDamage = 60;

        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.Hands;

        public override int CapacityCost => 3;

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.LightRed;
            Item.value = Item.sellPrice(0, 7, 0, 0);
        }

        /// <summary>
        /// 查询指定玩家是否装备了 <see cref="PlowSteelClampArm"/>，未装备返回 null
        /// </summary>
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
            //范围加成：原版的 tileRangeX/Y 是静态共享值，玩家私有的扩展量挂在 blockRange 上
            //同时打开 equippedAnyTileRangeAcc，让原版认为玩家持有"扩张配饰"，激活范围加成处理流程
            player.blockRange += TileRangeBonus;
            player.equippedAnyTileRangeAcc = true;

            //磁吸拾取：物品与金币都启用，确保"工业级抓取"语义
            player.treasureMagnet = true;
            player.goldRing = true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            string keyHint = CWRKeySystem.CyberwareSkill_Key?.GetAssignedKeys() is { Count: > 0 } keys
                ? $"[{keys[0]}]"
                : CWRLocText.Instance.Notbound.Value + $"[{CWRKeySystem.CyberwareSkill_Key?.DisplayName}]";
            tooltips.Add(new TooltipLine(Mod, "CyberwareSkillHint",
                Language.GetTextValue("Mods.CalamityOverhaul.Items.PlowSteelClampArm.SkillHint", keyHint)));
        }
    }
}
