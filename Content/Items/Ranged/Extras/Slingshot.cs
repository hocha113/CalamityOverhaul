using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Extras
{
    internal class Slingshot : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "Slingshot";
        public override void SetDefaults() {
            Item.width = Item.height = 22;
            Item.useTime = Item.useAnimation = 35;
            Item.damage = 6;
            Item.DamageType = DamageClass.Ranged;
            Item.rare = ItemRarityID.Blue;
            Item.value = Terraria.Item.buyPrice(0, 0, 0, 45);
            Item.SetHeldProj<SlingshotHeldProj>();
        }

        public override void AddRecipes() {
            _ = CreateRecipe().
                AddIngredient(ItemID.Wood, 6).
                AddIngredient(ItemID.Rope, 1).
                AddTile(TileID.WorkBenches).
                Register();
        }
    }
}
