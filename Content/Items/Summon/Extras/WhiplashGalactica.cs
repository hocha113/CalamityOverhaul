using CalamityMod.Items.Materials;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Summon.Whips;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon.Extras
{
    internal class WhiplashGalactica : ModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "WhiplashGalactica";

        public override void SetDefaults()
        {
            Item.DefaultToWhip(ModContent.ProjectileType<WhiplashGalacticaProjectile>(), 702, 0, 12, 45);
            Item.rare = ItemRarityID.Green;
            Item.channel = true;
        }

        public override bool MeleePrefix()
        {
            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<ElementWhip>())
                .AddIngredient<CosmiliteBar>(5)
                .AddTile(ModContent.TileType<CosmicAnvil>())
                .Register();
        }
    }
}
