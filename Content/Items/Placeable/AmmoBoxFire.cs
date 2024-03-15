using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class AmmoBoxFire : ModItem
    {
        public override string Texture => CWRConstant.Item + "Placeable/AmmoBox";
        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(6, 39));
        }

        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.value = 890;
            Item.useTime = Item.useAnimation = 22;
            Item.consumable = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.shoot = ModContent.ProjectileType<AmmoBoxProj>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            if (player.ownedProjectileCounts[Item.shoot] > 20) {
                CombatText.NewText(player.Hitbox, Color.Gold, CWRLocText.GetTextValue("AmmoBox_Text"));
                return false;
            }
            Vector2 pos = new Vector2((int)(Main.MouseWorld.X / 16), (int)(Main.MouseWorld.Y / 16)) * 16;
            Projectile.NewProjectile(Item.GetSource_FromThis(), pos, Vector2.Zero, type, 0, 0, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.AmmoBox)
                .AddIngredient(ItemID.EmptyBullet, 100)
                .AddIngredient(ItemID.LivingFireBlock, 5)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
