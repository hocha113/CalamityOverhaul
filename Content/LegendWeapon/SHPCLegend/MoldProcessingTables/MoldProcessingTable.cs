using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.MoldProcessingTables
{
    /// <summary>
    /// 模具加工台（放置物品）：右键已放置的物块以打开 <see cref="UI.MoldProcessingUI"/>
    /// </summary>
    internal class MoldProcessingTable : ModItem
    {
        //贴图沿用本目录下已有的 MoldProcessingTable.png
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

        public override void AddRecipes() {
            //占位配方：进入硬模后即可制作；后续可替换为更主题化（实验室电路 / 燃料芯）的材料
            CreateRecipe()
                .AddIngredient(ItemID.MythrilBar, 10)
                .AddIngredient(ItemID.Wire, 20)
                .AddIngredient(ItemID.HallowedBar, 5)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}
