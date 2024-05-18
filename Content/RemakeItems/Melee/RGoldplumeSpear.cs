using CalamityMod.Items;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RGoldplumeSpear : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.GoldplumeSpear>();
        public override int ProtogenesisID => ModContent.ItemType<GoldplumeSpearEcType>();
        public override string TargetToolTipItemName => "GoldplumeSpearEcType";
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[ModContent.ItemType<CalamityMod.Items.Weapons.Melee.GoldplumeSpear>()] = true;
        }

        public override void SetDefaults(Item item) {
            item.width = 54;
            item.damage = 24;
            item.DamageType = DamageClass.Melee;
            item.noMelee = true;
            item.useTurn = true;
            item.noUseGraphic = true;
            item.useAnimation = 23;
            item.useStyle = ItemUseStyleID.Shoot;
            item.useTime = 23;
            item.knockBack = 5.75f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.height = 54;
            item.value = CalamityGlobalItem.RarityOrangeBuyPrice;
            item.rare = ItemRarityID.Orange;
            item.shoot = ModContent.ProjectileType<RGoldplumeSpearProjectile>();
            item.shootSpeed = 8f;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int proj = Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            if (player.altFunctionUse == 2) {
                Main.projectile[proj].ai[1] = 1;
                Main.projectile[proj].rotation = player.Center.To(Main.MouseWorld).ToRotation();
            }
            return false;
        }

        public override bool? AltFunctionUse(Item item, Player player) {
            return true;
        }

        public override bool? UseItem(Item item, Player player) {
            return player.ownedProjectileCounts[item.shoot] == 0;
        }
    }
}
