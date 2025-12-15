using CalamityOverhaul.Content.Projectiles.Weapons.Summon.Whips;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon
{
    internal class ElementWhip : ModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "ElementWhip";

        public override void SetDefaults() {
            Item.DefaultToWhip(ModContent.ProjectileType<ElementWhipProjectile>(), 192, 2, 12, 30);
            Item.rare = ItemRarityID.Purple;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.value = Terraria.Item.buyPrice(0, 3, 5, 5);
            Item.rare = ItemRarityID.Pink;
        }

        public override bool MeleePrefix() {
            return true;
        }

        public override void AddRecipes() {
            _ = CreateRecipe()
                .AddIngredient(ItemID.RainbowWhip)
                .AddIngredient(ItemID.LunarBar, 5)
                .AddIngredient(CWRID.Item_LifeAlloy, 5)
                .AddIngredient(CWRID.Item_GalacticaSingularity, 5)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
