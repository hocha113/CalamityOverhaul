using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets.FlameTurrets
{
    /// <summary>火焰喷射塔,近距锥形持续喷火对群;贴图复用特斯拉塔,靠橙红色调区分</summary>
    internal class FlameTurret : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/TeslaElectromagneticTower";

        /// <summary>系列色调:橙红,同贴图靠它与特斯拉塔区分</summary>
        internal static readonly Color Tint = new(255, 140, 80);

        public static LocalizedText TurretOnText { get; private set; }
        public static LocalizedText TurretOffText { get; private set; }
        public static LocalizedText NoEnergyText { get; private set; }
        public static LocalizedText FuelLoadText { get; private set; }
        public static LocalizedText FuelTakeText { get; private set; }

        public override void SetStaticDefaults() {
            TurretOnText = this.GetLocalization(nameof(TurretOnText), () => "火焰塔启动");
            TurretOffText = this.GetLocalization(nameof(TurretOffText), () => "火焰塔关闭");
            NoEnergyText = this.GetLocalization(nameof(NoEnergyText), () => "能量不足!");
            FuelLoadText = this.GetLocalization(nameof(FuelLoadText), () => "装入凝胶 x{0}");
            FuelTakeText = this.GetLocalization(nameof(FuelTakeText), () => "取出凝胶 x{0}");
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
            Item.value = Item.buyPrice(0, 2, 20, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.color = Tint;
            Item.createTile = ModContent.TileType<FlameTurretTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }

        public override void AddRecipes() {
            CreateRecipe()
            .AddIngredient<CircuitBoard>(10)
            .AddIngredient(ItemID.HellstoneBar, 10)
            .AddIngredient(ItemID.Gel, 30)
            .AddTile(TileID.Anvils)
            .Register();

        }
    }
}
