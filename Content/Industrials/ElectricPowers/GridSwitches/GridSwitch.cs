using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.GridSwitches
{
    /// <summary>电网总闸,嵌进管道线路的电流开关;贴图暂复用激光输电装置物品,黄铜色调区分</summary>
    internal class GridSwitch : ModItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/LaserEnergyTrans";

        /// <summary>系列色调:配电黄铜</summary>
        internal static readonly Color Tint = new(228, 182, 96);

        public static LocalizedText OpenText { get; private set; }
        public static LocalizedText CloseText { get; private set; }

        public override void SetStaticDefaults() {
            OpenText = this.GetLocalization(nameof(OpenText), () => "已断开");
            CloseText = this.GetLocalization(nameof(CloseText), () => "已闭合");
        }

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 0, 40, 0);
            Item.rare = ItemRarityID.Green;
            Item.color = Tint;
            Item.createTile = ModContent.TileType<GridSwitchTile>();
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient(CWRID.Item_DubiousPlating, 10).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 10).
                AddRecipeGroup(CWRCrafted.TungstenBarGroup, 6).
                AddIngredient(ItemID.Wire, 5).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.TungstenBarGroup, 10).
                AddIngredient(ItemID.Wire, 5).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }
}
