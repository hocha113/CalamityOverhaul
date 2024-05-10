using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RHalibutCannon : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.HalibutCannon>();
        public override int ProtogenesisID => ModContent.ItemType<HalibutCannonEcType>();
        public override string TargetToolTipItemName => "HalibutCannonEcType";
        public override void SetDefaults(Item item) {
            item.damage = 3;
            item.DamageType = DamageClass.Ranged;
            item.width = 118;
            item.height = 56;
            item.useTime = 10;
            item.useAnimation = 20;
            item.useStyle = ItemUseStyleID.Shoot;
            item.rare = ModContent.RarityType<HotPink>();
            item.noMelee = true;
            item.knockBack = 1f;
            item.value = CalamityGlobalItem.RarityHotPinkBuyPrice;
            item.UseSound = SoundID.Item38;
            item.autoReuse = true;
            item.shoot = ProjectileID.Bullet;
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<HalibutCannonHeldProj>();
        }

        public override bool? On_ModifyWeaponCrit(Item item, Player player, ref float crit) {
            crit += HalibutCannonEcType.GetOnCrit;
            return false;
        }

        public override bool On_ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            damage *= HalibutCannonEcType.GetOnDamage / (float)HalibutCannonEcType.GetStartDamage;
            return false;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            // 创建一个新的集合以防修改 tooltips 集合时产生异常
            List<TooltipLine> newTooltips = new List<TooltipLine>(tooltips);
            List<TooltipLine> prefixTooltips = new List<TooltipLine>();
            // 遍历 tooltips 集合并隐藏特定的提示行
            foreach (TooltipLine line in newTooltips.ToList()) {
                for (int i = 0; i < 9; i++) {
                    if (line.Name == "Tooltip" + i) {
                        line.Hide();
                    }
                }
                if (line.Name.Contains("Prefix")) {
                    prefixTooltips.Add(line.Clone());
                    line.Hide();
                }
            }

            // 清空原 tooltips 集合并添加修改后的新Tooltips集合
            tooltips.Clear();
            tooltips.AddRange(newTooltips);
            tooltips.AddRange(prefixTooltips);
        }
    }
}
