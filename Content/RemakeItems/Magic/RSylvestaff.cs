using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Magic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RSylvestaff : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Sylvestaff>();
        public override bool DrawingInfo => false;
        public override void ModifyRecipe(Recipe recipe) {
            recipe.RemoveIngredient(ItemID.GenderChangePotion);
            recipe.RemoveIngredient(ItemID.GoldBar);
            recipe.RemoveIngredient(ModContent.ItemType<ShadowspecBar>());
            recipe.AddIngredient(ItemID.SoulofLight, 10);
            recipe.AddIngredient(ItemID.PixieDust, 12);
            recipe.AddIngredient(ItemID.CrystalShard, 10);
            recipe.AddIngredient(ItemID.Moonglow, 2);
            recipe.AddIngredient(ModContent.ItemType<ShadowspecBar>(), 5);
        }
    }
}
