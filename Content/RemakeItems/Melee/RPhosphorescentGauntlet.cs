using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.PhosphorescentGauntletProj;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RPhosphorescentGauntlet : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.PhosphorescentGauntlet>();
        public override int ProtogenesisID => ModContent.ItemType<PhosphorescentGauntletEcType>();
        public override string TargetToolTipItemName => "PhosphorescentGauntletEcType";

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        }

        public override void SetDefaults(Item item) {
            item.width = item.height = 40;
            item.damage = 2705;
            item.DamageType = ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>();
            item.noMelee = true;
            item.noUseGraphic = true;
            item.channel = true;
            item.useAnimation = item.useTime = 40;
            item.useTurn = true;
            item.useStyle = ItemUseStyleID.Swing;
            item.knockBack = 9f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.value = CalamityGlobalItem.RarityPureGreenBuyPrice;
            item.shoot = ModContent.ProjectileType<PhosphorescentGauntletPunches>();
            item.shootSpeed = 1f;
            item.rare = ModContent.RarityType<PureGreen>();
        }

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? UseItem(Item item, Player player) {
            item.useAnimation = item.useTime = 20;
            item.useStyle = ItemUseStyleID.Shoot;
            item.shootSpeed = 15;
            if (player.altFunctionUse == 2) {
                item.useAnimation = item.useTime = 40;
                item.useStyle = ItemUseStyleID.Swing;
                item.shootSpeed = 1;
            }
            return base.UseItem(item, player);
        }

        public override void HoldItem(Item item, Player player) {
            if (player.whoAmI == Main.myPlayer)
                player.direction = Math.Sign(player.Center.To(Main.MouseWorld).X);
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
                return false;
            }

            Projectile.NewProjectile(source, position, velocity.UnitVector() * 15, ModContent.ProjectileType<PGProj>(), damage / 3, knockback / 2, player.whoAmI);
            return false;
        }
    }
}
