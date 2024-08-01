using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 海爵剑
    /// </summary>
    internal class BrinyBaronEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "BrinyBaron";

        private int Level;
        public override void SetDefaults() => SetDefaultsFunc(Item);
        public static void SetDefaultsFunc(Item Item) {
            Item.damage = 110;
            Item.knockBack = 2f;
            Item.useAnimation = Item.useTime = 20;
            Item.DamageType = DamageClass.Melee;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.shootSpeed = 4f;
            Item.shoot = ModContent.ProjectileType<Razorwind>();
            Item.width = 100;
            Item.height = 102;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.SetKnifeHeld<BrinyBaronHeld>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(ref Level, player, source, position, velocity, type, damage, knockback);
        }

        public static bool ShootFunc(ref int Level, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int newLevel = 0;
            if (++Level > 6) {
                newLevel = Level - 6;
                if (Level > 8) {
                    Level = 0;
                }
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, newLevel);
            return false;
        }
    }
}
