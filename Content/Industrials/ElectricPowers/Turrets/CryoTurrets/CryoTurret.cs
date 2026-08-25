using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets.CryoTurrets
{
    /// <summary>冰冻塔,范围减速与蓄冻控制;贴图复用特斯拉塔,靠冰蓝色调区分</summary>
    internal class CryoTurret : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/TeslaElectromagneticTower";

        /// <summary>系列色调:冰蓝,同贴图靠它与特斯拉塔区分</summary>
        internal static readonly Color Tint = new(150, 210, 255);

        public static LocalizedText TurretOnText { get; private set; }
        public static LocalizedText TurretOffText { get; private set; }
        public static LocalizedText NoEnergyText { get; private set; }

        public override void SetStaticDefaults() {
            TurretOnText = this.GetLocalization(nameof(TurretOnText), () => "冰冻塔启动");
            TurretOffText = this.GetLocalization(nameof(TurretOffText), () => "冰冻塔关闭");
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
            Item.value = Item.buyPrice(0, 2, 60, 0);
            Item.rare = ItemRarityID.Pink;
            Item.color = Tint;
            Item.createTile = ModContent.TileType<CryoTurretTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe()
                    .AddIngredient(CWRID.Item_DubiousPlating, 12)
                    .AddIngredient(CWRID.Item_MysteriousCircuitry, 10)
                    .AddIngredient(ItemID.FrostCore, 1)
                    .AddIngredient(ItemID.IceBlock, 50)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
            else {
                CreateRecipe()
                    .AddRecipeGroup(CWRCrafted.TungstenBarGroup, 12)
                    .AddIngredient(ItemID.FrostCore, 1)
                    .AddIngredient(ItemID.IceBlock, 50)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
        }
    }
}
