using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.AutoFishers
{
    /// <summary>自动钓鱼机,消耗鱼饵与电力从水面自动收获渔获</summary>
    internal class AutoFisher : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/AutoFisher";

        /// <summary>系列色调:湖蓝,用于提示文本与 UI 点缀</summary>
        internal static readonly Color Tint = new(135, 200, 240);

        public static LocalizedText NoEnergyText { get; private set; }
        public static LocalizedText NoBaitText { get; private set; }
        public static LocalizedText NoWaterText { get; private set; }
        public static LocalizedText FullText { get; private set; }

        public override void SetStaticDefaults() {
            NoEnergyText = this.GetLocalization(nameof(NoEnergyText), () => "能量不足!");
            NoBaitText = this.GetLocalization(nameof(NoBaitText), () => "没有鱼饵!");
            NoWaterText = this.GetLocalization(nameof(NoWaterText), () => "附近没有可垂钓的水面!");
            FullText = this.GetLocalization(nameof(FullText), () => "渔获仓已满!");
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
            Item.value = Item.buyPrice(0, 2, 40, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.createTile = ModContent.TileType<AutoFisherTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }

        public override void AddRecipes() {
            CreateRecipe()
            .AddIngredient<CircuitBoard>(8)
            .AddRecipeGroup(CWRCrafted.TungstenBarGroup, 8)
            .AddIngredient(ItemID.WoodFishingPole, 1)
            .AddIngredient(ItemID.Cobweb, 20)
            .AddTile(TileID.Anvils)
            .Register();

        }
    }
}
