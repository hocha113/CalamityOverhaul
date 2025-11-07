using CalamityMod.Items.Materials;
using CalamityMod.Tiles.Furniture.CraftingStations;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    internal static class CWRID
    {
        public static int ExoPrism => ModContent.ItemType<ExoPrism>();
        public static int Item_DraedonsForge => ModContent.ItemType<CalamityMod.Items.Placeables.Furniture.CraftingStations.DraedonsForge>();
        public static int Tile_DraedonsForge => ModContent.TileType<DraedonsForge>();
    }
}
