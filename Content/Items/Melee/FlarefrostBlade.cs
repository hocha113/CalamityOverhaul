using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria.DataStructures;
using CalamityOverhaul.Common;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 冰与火之刃
    /// </summary>
    internal class FlarefrostBlade : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "FlarefrostBlade";
        public int shootCount;
        public int useBigBoom = 8;
        public override void SetDefaults() {
            Item.width = 64;
            Item.damage = 125;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 16;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Rapier;
            Item.knockBack = 6.25f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 66;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.value = CalamityGlobalItem.Rarity5BuyPrice;
            Item.rare = ItemRarityID.Pink;
            Item.shoot = ModContent.ProjectileType<RFlarefrostBladeHeldProj>();
            Item.shootSpeed = 1f;
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (shootCount >= useBigBoom) //使用强力挥舞
            {
                Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI, 3);

                shootCount = 0;
                useBigBoom = Main.rand.Next(6, 9);
            }
            else {
                Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI, shootCount % 3);
                //Projectile.NewProjectile(source, player.Center, (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX) * 10f,
                //    ModContent.ProjectileType<RedJadeStrike>(), (int)(damage * 0.75f), knockback, player.whoAmI, Main.rand.Next(3));
            }

            shootCount++;
            return false;
        }
    }
}
