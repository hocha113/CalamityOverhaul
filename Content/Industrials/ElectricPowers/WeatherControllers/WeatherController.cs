using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.WeatherControllers
{
    /// <summary>天气控制机,大额耗电求雨或止雨;贴图复用热能电池,靠雨云蓝色调区分</summary>
    internal class WeatherController : ModItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/ThermalBatteryLegacy";

        /// <summary>系列色调:雨云蓝</summary>
        internal static readonly Color Tint = new(135, 175, 250);

        public static LocalizedText NoEnergyText { get; private set; }
        public static LocalizedText RainBroadcast { get; private set; }
        public static LocalizedText ClearBroadcast { get; private set; }

        public override void SetStaticDefaults() {
            NoEnergyText = this.GetLocalization(nameof(NoEnergyText), () => "能量不足!");
            RainBroadcast = this.GetLocalization(nameof(RainBroadcast), () => "{0} 启动了天气控制机,雨云正在聚集");
            ClearBroadcast = this.GetLocalization(nameof(ClearBroadcast), () => "{0} 启动了天气控制机,雨过天晴");
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
            Item.value = Item.buyPrice(0, 3, 0, 0);
            Item.rare = ItemRarityID.Pink;
            Item.color = Tint;
            Item.createTile = ModContent.TileType<WeatherControllerTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 1000;
        }

        public override void AddRecipes() {
            CreateRecipe().
            AddIngredient(ItemID.HallowedBar, 8).
            AddIngredient(ItemID.Cloud, 30).
            AddIngredient<CircuitBoard>(15).
            AddTile(TileID.MythrilAnvil).
            Register();

        }
    }
}
