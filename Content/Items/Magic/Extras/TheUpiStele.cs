using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Extras
{
    internal class TheUpiStele : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "TheUpiStele";
        public override void SetDefaults() {
            Item.damage = 6;
            Item.knockBack = 1f;
            Item.useTime = 32;
            Item.useAnimation = 32;
            Item.DamageType = DamageClass.Magic;
            Item.width = 46;
            Item.height = 46;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.value = Terraria.Item.buyPrice(0, 0, 0, 15);
            Item.rare = ItemRarityID.White;
            Item.UseSound = SoundID.Item9;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shootSpeed = 7;
            Item.mana = 20;
            Item.shoot = ModContent.ProjectileType<MagicStar>();
            Item.SetHeldProj<TheUpiSteleHeldProj>();
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<Pebble>(6).
                AddIngredient(ItemID.FallenStar, 6).
                AddTile(TileID.WorkBenches).
                Register();
        }
    }
}
