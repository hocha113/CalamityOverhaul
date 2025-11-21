using CalamityOverhaul.Content.Projectiles.Weapons.Melee.PhosphorescentGauntletProj;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RPhosphorescentGauntlet : CWRItemOverride
    {
        public const int OnHitIFrames = 15;
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        public override void SetDefaults(Item item) => item.damage = 1205;
        public override void HoldItem(Item item, Player player) => HoldItemFunc(player);
        public override bool? AltFunctionUse(Item item, Player player) => true;
        public override bool? On_CanUseItem(Item item, Player player) => CanUseItemFunc(player, item);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => ShootFunc(player, source, position, velocity, type, damage, knockback);

        public static void HoldItemFunc(Player player) {
            if (!player.PressKey()) {
                return;
            }
            player.direction = Math.Sign(player.Center.To(Main.MouseWorld).X);
        }

        public static bool CanUseItemFunc(Player player, Item Item) {
            Item.useAnimation = Item.useTime = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.shootSpeed = 15;
            if (player.altFunctionUse == 2) {
                Item.useAnimation = Item.useTime = 40;
                Item.useStyle = ItemUseStyleID.Swing;
                Item.shootSpeed = 1;
            }

            return player.ownedProjectileCounts[Item.shoot] <= 0;
        }

        public static bool ShootFunc(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, type, damage * 2, knockback, player.whoAmI);
                return false;
            }
            Projectile.NewProjectile(source, position, velocity.UnitVector() * 15, ModContent.ProjectileType<GauntletInAltShoot>(), damage / 3, knockback / 2, player.whoAmI);
            return false;
        }
    }
}
