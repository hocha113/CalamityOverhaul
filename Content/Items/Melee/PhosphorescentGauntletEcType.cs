using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.PhosphorescentGauntletProj;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 磷光拳套
    /// </summary>
    internal class PhosphorescentGauntletEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "PhosphorescentGauntlet";
        public const int OnHitIFrames = 15;
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        public override void SetDefaults() {
            Item.SetItemCopySD<PhosphorescentGauntlet>();
            Item.damage = 1205;
        }
        public override void HoldItem(Player player) => HoldItemFunc(player);
        public static void HoldItemFunc(Player player) {
            if (!player.PressKey()) {
                return;
            }
            player.direction = Math.Sign(player.Center.To(Main.MouseWorld).X);
        }
        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 10;
        public override bool AltFunctionUse(Player player) => true;
        public override bool CanUseItem(Player player) => CanUseItemFunc(player, Item);
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

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => ShootFunc(player, source, position, velocity, type, damage, knockback);
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
