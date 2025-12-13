using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RSylvestaff : CWRItemOverride
    {
        public override bool DrawingInfo => false;
        public override void ModifyRecipe(Recipe recipe) {
            recipe.RemoveIngredient(ItemID.GenderChangePotion);
            recipe.RemoveIngredient(ItemID.GoldBar);
            recipe.RemoveIngredient(CWRID.Item_ShadowspecBar);
            recipe.AddIngredient(ItemID.SoulofLight, 10);
            recipe.AddIngredient(ItemID.PixieDust, 12);
            recipe.AddIngredient(ItemID.CrystalShard, 10);
            recipe.AddIngredient(ItemID.Moonglow, 2);
            recipe.AddIngredient(CWRID.Item_ShadowspecBar, 5);
        }
    }
}
