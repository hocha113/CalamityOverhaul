using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Apiaries
{
    /// <summary>电动养蜂箱,消耗空玻璃瓶周期性灌装蜂蜜瓶</summary>
    internal class Apiary : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/Apiary";

        /// <summary>系列色调:蜂蜜金,用于提示文本与产出辉光</summary>
        internal static readonly Color Tint = new(235, 168, 50);

        public static LocalizedText NoEnergyText { get; private set; }
        public static LocalizedText NoBottleText { get; private set; }
        public static LocalizedText FullText { get; private set; }

        public override void SetStaticDefaults() {
            NoEnergyText = this.GetLocalization(nameof(NoEnergyText), () => "能量不足!");
            NoBottleText = this.GetLocalization(nameof(NoBottleText), () => "没有空瓶!");
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
            Item.value = Item.buyPrice(0, 1, 0, 0);
            Item.rare = ItemRarityID.Orange;
            Item.createTile = ModContent.TileType<ApiaryTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 300;
        }

        public override void AddRecipes() {
            CreateRecipe()
            .AddIngredient<CircuitBoard>(6)
            .AddIngredient(ItemID.Hive, 10)
            .AddIngredient(ItemID.Bottle, 3)
            .AddRecipeGroup(CWRCrafted.TinBarGroup, 8)
            .AddTile(TileID.Anvils)
            .Register();

        }
    }
}
