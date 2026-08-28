using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.SlimeVats
{
    /// <summary>史莱姆培养槽,耗水耗电周期性培养凝胶;贴图复用生命编织者,靠凝胶绿色调区分</summary>
    internal class SlimeVat : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/LifeWeaver";

        /// <summary>系列色调:凝胶绿</summary>
        internal static readonly Color Tint = new(96, 206, 120);

        public static LocalizedText NoEnergyText { get; private set; }
        public static LocalizedText NoWaterText { get; private set; }
        public static LocalizedText FullText { get; private set; }
        public static LocalizedText PourText { get; private set; }

        public override void SetStaticDefaults() {
            NoEnergyText = this.GetLocalization(nameof(NoEnergyText), () => "能量不足!");
            NoWaterText = this.GetLocalization(nameof(NoWaterText), () => "水量不足!");
            FullText = this.GetLocalization(nameof(FullText), () => "产出仓已满!");
            PourText = this.GetLocalization(nameof(PourText), () => "+{0} 水");
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
            Item.color = Tint;
            Item.createTile = ModContent.TileType<SlimeVatTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 500;
        }

        public override void AddRecipes() {
            CreateRecipe()
            .AddIngredient<CircuitBoard>(6)
            .AddIngredient(ItemID.Gel, 30)
            .AddIngredient(ItemID.Glass, 8)
            .AddRecipeGroup(CWRCrafted.TinBarGroup, 10)
            .AddTile(TileID.Anvils)
            .Register();

        }
    }
}
