using CalamityMod;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class TheBurningSkyEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "TheBurningSky";
        public override void SetDefaults() => SetDefaultsFunc(Item);
        internal static void SetDefaultsFunc(Item Item) {
            Item.width = 74;
            Item.height = 74;
            Item.value = Terraria.Item.sellPrice(gold: 75);
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useAnimation = 32;
            Item.useTime = 32;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.damage = 950;
            Item.crit = 16;
            Item.knockBack = 7.5f;
            Item.noUseGraphic = true;
            Item.DamageType = ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>();
            Item.noMelee = true;
            Item.channel = true;
            Item.shootSpeed = 10f;
            Item.shoot = ModContent.ProjectileType<TheBurningSkyHeld>();
            Item.rare = ModContent.RarityType<Violet>();
        }
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(Item, player, source, position, velocity, type, damage, knockback);
        }
        internal static bool ShootFunc(Item Item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type
                , damage, knockback, player.whoAmI, 0, 0, player.GetAdjustedItemScale(Item));
            return false;
        }
    }
}
