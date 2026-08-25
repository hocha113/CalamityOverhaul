using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.HealingStations
{
    /// <summary>治疗站,为范围内玩家提供再生光环;贴图复用特斯拉塔,靠暖粉色调区分</summary>
    internal class HealStation : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/TeslaElectromagneticTower";

        /// <summary>系列色调:暖粉,同贴图靠它与特斯拉塔区分</summary>
        internal static readonly Color Tint = new(255, 160, 190);

        public static LocalizedText FieldOnText { get; private set; }
        public static LocalizedText FieldOffText { get; private set; }
        public static LocalizedText NoEnergyText { get; private set; }

        public override void SetStaticDefaults() {
            FieldOnText = this.GetLocalization(nameof(FieldOnText), () => "治疗光环启动");
            FieldOffText = this.GetLocalization(nameof(FieldOffText), () => "治疗光环关闭");
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
            Item.value = Item.buyPrice(0, 1, 60, 0);
            Item.rare = ItemRarityID.Orange;
            Item.color = Tint;
            Item.createTile = ModContent.TileType<HealStationTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 500;
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe()
                    .AddIngredient(CWRID.Item_DubiousPlating, 10)
                    .AddIngredient(CWRID.Item_MysteriousCircuitry, 8)
                    .AddIngredient(ItemID.LifeCrystal, 1)
                    .AddIngredient(ItemID.HealingPotion, 5)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
            else {
                CreateRecipe()
                    .AddRecipeGroup(CWRCrafted.TungstenBarGroup, 12)
                    .AddIngredient(ItemID.LifeCrystal, 1)
                    .AddIngredient(ItemID.HealingPotion, 5)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
        }
    }
}
