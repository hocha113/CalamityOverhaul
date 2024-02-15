using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 真红手炮
    /// </summary>
    internal class ClaretCannon : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ClaretCannon";
        public new string LocalizationCategory => "Items.Weapons.Ranged";
        public override void SetDefaults() {
            Item.damage = 140;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 48;
            Item.height = 30;
            Item.useTime = 3;
            Item.useAnimation = 9;
            Item.reuseDelay = 10;
            Item.useLimitPerAnimation = 3;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 5.5f;
            Item.value = CalamityGlobalItem.Rarity12BuyPrice;
            Item.rare = ModContent.RarityType<Turquoise>();
            Item.UseSound = SoundID.Item40;
            Item.autoReuse = true;
            Item.shootSpeed = 24f;
            Item.shoot = ModContent.ProjectileType<ClaretCannonProj>();
            Item.useAmmo = AmmoID.Bullet;
            Item.Calamity().canFirePointBlankShots = true;
            
        }

        public override Vector2? HoldoutOffset() => new Vector2(-5, 0);

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (type == ProjectileID.Bullet) {
                int proj = Projectile.NewProjectile(source, position.X, position.Y, velocity.X, velocity.Y, ModContent.ProjectileType<ClaretCannonProj>(), damage, knockback, player.whoAmI);
                Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.ClaretCannon;
            }
            else {
                Projectile.NewProjectile(source, position.X, position.Y, velocity.X, velocity.Y, type, damage, knockback, player.whoAmI);
            }
            return false;
        }
    }
}
