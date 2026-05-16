using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Starships
{
    internal class Starship : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "Starship";
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 94;
            Item.height = 34;
            Item.damage = 186;
            Item.useTime = 4;
            Item.useAnimation = 4;
            Item.useAmmo = AmmoID.Bullet;
            Item.shootSpeed = 18f;
            Item.knockBack = 2.5f;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.autoReuse = true;
            Item.UseSound = null;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(1, 80, 0, 0);
            Item.SetHeldProj<StarshipHeld>();
        }

        public override void AddRecipes() {
            if (CWRRef.Has && CWRID.Item_Starmada > 0 && CWRID.Item_ShadowspecBar > 0) {
                CreateRecipe().AddIngredient(CWRID.Item_Starmada).AddIngredient(CWRID.Item_ShadowspecBar, 5).AddIngredient(CWRID.Item_Rock).AddTile(CWRID.Tile_DraedonsForge).Register();
            }
        }
    }
}
