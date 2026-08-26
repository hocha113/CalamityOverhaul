using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.HerbFarmers
{
    /// <summary>草药农场机,自动播种与收割范围内的草药</summary>
    internal class HerbFarmer : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/HerbFarmer";

        /// <summary>系列色调:麦浪暖黄,用于提示文本与 UI 点缀</summary>
        internal static readonly Color Tint = new(230, 220, 130);

        public static LocalizedText NoEnergyText { get; private set; }

        public override void SetStaticDefaults() {
            NoEnergyText = this.GetLocalization(nameof(NoEnergyText), () => "能量不足!");
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
            Item.value = Item.buyPrice(0, 1, 20, 0);
            Item.rare = ItemRarityID.Orange;
            Item.createTile = ModContent.TileType<HerbFarmerTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 500;
        }

        public override void AddRecipes() {
                CreateRecipe()
                .AddIngredient<CircuitBoard>(6)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 10)
                .AddIngredient(ItemID.Sunflower, 2)
                .AddTile(TileID.Anvils)
                .Register();

        }
    }
}
