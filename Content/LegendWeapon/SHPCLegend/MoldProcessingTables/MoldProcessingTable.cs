using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.MoldProcessingTables
{
    /// <summary>模具加工台放置物，右键开 <see cref="UI.MoldProcessingUI"/></summary>
    internal class MoldProcessingTable : ModItem
    {
        public override string Texture => "CalamityOverhaul/Content/LegendWeapon/SHPCLegend/MoldProcessingTables/MoldProcessingTable";

        public override void SetDefaults() {
            Item.width = 64;
            Item.height = 48;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 10;
            Item.useAnimation = 15;
            Item.consumable = true;
            Item.rare = ItemRarityID.LightRed;
            Item.value = Item.sellPrice(0, 5, 0, 0);
            Item.createTile = ModContent.TileType<MoldProcessingTableTile>();
        }
    }
}
