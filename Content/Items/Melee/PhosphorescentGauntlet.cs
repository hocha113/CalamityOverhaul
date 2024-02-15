using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Rarities;
using CalamityMod;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.PhosphorescentGauntletProj;
using System;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 磷光拳套
    /// </summary>
    internal class PhosphorescentGauntlet : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "PhosphorescentGauntlet";
        public new string LocalizationCategory => "Items.Weapons.Melee";
        public const int OnHitIFrames = 15;

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = Item.height = 40;
            Item.damage = 2705;
            Item.DamageType = ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>();
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.useAnimation = Item.useTime = 40;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 9f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.RarityPureGreenBuyPrice;
            Item.shoot = ModContent.ProjectileType<PhosphorescentGauntletPunches>();
            Item.shootSpeed = 1f;
            Item.rare = ModContent.RarityType<PureGreen>();
            
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 10;

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;

        public override bool? UseItem(Player player) {
            Item.useAnimation = Item.useTime = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.shootSpeed = 15;
            if (player.altFunctionUse == 2) {
                Item.useAnimation = Item.useTime = 40;
                Item.useStyle = ItemUseStyleID.Swing;
                Item.shootSpeed = 1;
            }

            return base.UseItem(player);
        }

        public override void HoldItem(Player player) {
            if (player.whoAmI == Main.myPlayer)
                player.direction = Math.Sign(player.Center.To(Main.MouseWorld).X);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
                return false;
            }
            
            Projectile.NewProjectile(source, position, velocity.UnitVector() * 15, ModContent.ProjectileType<PGProj>(), damage / 3, knockback / 2, player.whoAmI);
            return false;
        }
    }
}
