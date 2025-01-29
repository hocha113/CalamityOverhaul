using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTheBurningSky : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<TheBurningSky>();
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(item, player, source, position, velocity, type, damage, knockback);
        }

        internal static void SetDefaultsFunc(Item Item) {
            Item.width = 74;
            Item.height = 74;
            Item.useAnimation = 32;
            Item.useTime = 32;
            Item.damage = 950;
            Item.crit = 16;
            Item.knockBack = 7.5f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.channel = true;
            Item.shootSpeed = 10f;
            Item.value = Item.sellPrice(gold: 75);
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.shoot = ModContent.ProjectileType<TheBurningSkyHeld>();
            Item.rare = ModContent.RarityType<Violet>();
        }

        internal static bool ShootFunc(Item Item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type
                , damage, knockback, player.whoAmI, 0, 0, player.GetAdjustedItemScale(Item));
            return false;
        }
    }
}
