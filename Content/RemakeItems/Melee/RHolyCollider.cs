using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RHolyCollider : CWRItemOverride
    {
        private int Level;
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(ref Level, item, player, source, position, velocity, type, damage, knockback);
        }

        internal static void SetDefaultsFunc(Item Item) {
            Item.width = 94;
            Item.height = 80;
            Item.scale = 1f;
            Item.damage = 1270;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 30;
            Item.useTime = 30;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.knockBack = 3.75f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<HolyColliderHeld>();
            Item.shootSpeed = 10f;
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Item.type] = true;
            ItemMeleePrefixDic[Item.type] = true;
        }

        internal static bool ShootFunc(ref int Level, Item Item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int newLevel = 0;
            if (++Level > 6) {
                newLevel = 2;
                Level = 0;
            }
            Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<HolyColliderHeld>(), damage, knockback, player.whoAmI, newLevel);
            return false;
        }
    }
}
