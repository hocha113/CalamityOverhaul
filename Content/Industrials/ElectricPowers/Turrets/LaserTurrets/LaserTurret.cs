using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets.LaserTurrets
{
    /// <summary>激光塔,远距单体狙击;贴图复用特斯拉塔,靠猩红色调区分</summary>
    internal class LaserTurret : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/TeslaElectromagneticTower";

        /// <summary>系列色调:猩红,同贴图靠它与特斯拉塔区分</summary>
        internal static readonly Color Tint = new(255, 95, 95);

        public static LocalizedText TurretOnText { get; private set; }
        public static LocalizedText TurretOffText { get; private set; }
        public static LocalizedText NoEnergyText { get; private set; }

        public override void SetStaticDefaults() {
            TurretOnText = this.GetLocalization(nameof(TurretOnText), () => "激光塔启动");
            TurretOffText = this.GetLocalization(nameof(TurretOffText), () => "激光塔关闭");
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
            Item.value = Item.buyPrice(0, 3, 20, 0);
            Item.rare = ItemRarityID.LightPurple;
            Item.color = Tint;
            Item.createTile = ModContent.TileType<LaserTurretTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 1200;
        }

        public override void AddRecipes() {
                CreateRecipe()
                .AddIngredient<CircuitBoard>(15)
                .AddIngredient(ItemID.SoulofMight, 10)
                .AddIngredient(ItemID.CrystalShard, 20)
                .AddTile(TileID.Anvils)
                .Register();

        }
    }
}
