using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.MushroomFarmers
{
    /// <summary>蘑菇农场机,在草地与蘑菇草上自动培育并采收蘑菇</summary>
    internal class MushroomFarmer : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/MushroomFarmer";

        /// <summary>系列色调:菌蓝紫,用于提示文本与 UI 点缀</summary>
        internal static readonly Color Tint = new(150, 140, 235);

        public static LocalizedText NoEnergyText { get; private set; }
        public static LocalizedText FullText { get; private set; }

        public override void SetStaticDefaults() {
            NoEnergyText = this.GetLocalization(nameof(NoEnergyText), () => "能量不足!");
            FullText = this.GetLocalization(nameof(FullText), () => "产出仓已满!");
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
            Item.createTile = ModContent.TileType<MushroomFarmerTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 500;
        }

        public override void AddRecipes() {
            CreateRecipe()
            .AddIngredient<CircuitBoard>(6)
            .AddRecipeGroup(CWRCrafted.TinBarGroup, 10)
            .AddIngredient(ItemID.Mushroom, 5)
            .AddIngredient(ItemID.GlowingMushroom, 5)
            .AddTile(TileID.Anvils)
            .Register();

        }
    }
}
