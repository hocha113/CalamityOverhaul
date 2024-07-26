using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 圣火巨刃
    /// </summary>
    internal class HolyColliderEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "HolyCollider";
        int Level;
        public override void SetDefaults() {
            SetDefaultsFunc(Item);
        }

        internal static void SetDefaultsFunc(Item Item) {
            Item.width = 94;
            Item.height = 80;
            Item.scale = 1f;
            Item.damage = 230;
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
            Item.value = CalamityGlobalItem.RarityTurquoiseBuyPrice;
            Item.rare = ModContent.RarityType<Turquoise>();
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Item.type] = true;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(ref Level, Item, player, source, position, velocity, type, damage, knockback);
        }

        internal static bool ShootFunc(ref int Level, Item Item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int newLevel = 0;
            if (++Level > 6) {
                newLevel = 2;
                Level = 0;
            }
            if (player.altFunctionUse == 2) {
                newLevel = 1;
            }
            Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<HolyColliderHeld>(), damage, knockback, player.whoAmI, newLevel);
            return false;
        }
    }
}
