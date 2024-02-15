using CalamityMod.Items.Materials;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Summon.Whips;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon.Extras
{
    internal class ElementWhip : ModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "ElementWhip";

        public override void SetDefaults()
        {
            Item.DefaultToWhip(ModContent.ProjectileType<ElementWhipProjectile>(), 192, 2, 12, 30);
            Item.rare = ItemRarityID.Purple;
        }

        public override bool MeleePrefix()
        {
            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.RainbowWhip)
                .AddIngredient(ItemID.LunarBar, 5)
                .AddIngredient<LifeAlloy>(5)
                .AddIngredient<GalacticaSingularity>(5)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
