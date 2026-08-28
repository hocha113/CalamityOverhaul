using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ShieldGenerators
{
    /// <summary>护盾发生器,为范围内玩家提供吸收护盾;贴图复用特斯拉塔,靠青紫色调区分</summary>
    internal class ShieldGenerator : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/TeslaElectromagneticTower";

        /// <summary>系列色调:青紫,同贴图靠它与特斯拉塔区分</summary>
        internal static readonly Color Tint = new(170, 160, 255);

        public static LocalizedText FieldOnText { get; private set; }
        public static LocalizedText FieldOffText { get; private set; }
        public static LocalizedText NoEnergyText { get; private set; }

        public override void SetStaticDefaults() {
            FieldOnText = this.GetLocalization(nameof(FieldOnText), () => "护盾力场启动");
            FieldOffText = this.GetLocalization(nameof(FieldOffText), () => "护盾力场关闭");
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
            Item.value = Item.buyPrice(0, 2, 80, 0);
            Item.rare = ItemRarityID.Pink;
            Item.color = Tint;
            Item.createTile = ModContent.TileType<ShieldGeneratorTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }

        public override void AddRecipes() {
            CreateRecipe()
            .AddIngredient<CircuitBoard>(10)
            .AddIngredient(ItemID.CrystalShard, 15)
            .AddIngredient(ItemID.SoulofLight, 8)
            .AddTile(TileID.Anvils)
            .Register();

        }
    }
}
