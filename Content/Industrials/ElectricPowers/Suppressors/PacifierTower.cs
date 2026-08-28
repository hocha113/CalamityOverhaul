using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Suppressors
{
    /// <summary>宁静力场发生器,通电时压制范围内的自然刷怪;贴图复用特斯拉塔,靠冷绿色调区分</summary>
    internal class PacifierTower : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/TeslaElectromagneticTower";

        /// <summary>系列色调:冷绿,同贴图靠它与特斯拉塔区分</summary>
        internal static readonly Color Tint = new(150, 235, 175);

        public static LocalizedText FieldOnText { get; private set; }
        public static LocalizedText FieldOffText { get; private set; }
        public static LocalizedText NoEnergyText { get; private set; }

        public override void SetStaticDefaults() {
            FieldOnText = this.GetLocalization(nameof(FieldOnText), () => "力场启动");
            FieldOffText = this.GetLocalization(nameof(FieldOffText), () => "力场关闭");
            NoEnergyText = this.GetLocalization(nameof(NoEnergyText), () => "能量不足!");
        }

        public override void SetDefaults() {
            Item.width = 38;
            Item.height = 78;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 2, 0, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.color = Tint;
            Item.createTile = ModContent.TileType<PacifierTowerTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }

        public override void AddRecipes() {
            CreateRecipe()
            .AddIngredient<CircuitBoard>(10)
            .AddRecipeGroup(CWRCrafted.TungstenBarGroup, 12)
            .AddIngredient(ItemID.PeaceCandle, 3)
            .AddTile(TileID.Anvils)
            .Register();

        }
    }
}
