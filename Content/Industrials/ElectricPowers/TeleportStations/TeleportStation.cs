using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.TeleportStations
{
    /// <summary>传送站,与世界上的同类站点组成传送网络;贴图复用热能电池,靠传送青色调区分</summary>
    internal class TeleportStation : ModItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/ThermalBattery";

        /// <summary>系列色调:传送青</summary>
        internal static readonly Color Tint = new(110, 235, 215);

        public static LocalizedText NoEnergyText { get; private set; }
        public static LocalizedText TargetNoEnergyText { get; private set; }
        public static LocalizedText UnnamedText { get; private set; }

        public override void SetStaticDefaults() {
            NoEnergyText = this.GetLocalization(nameof(NoEnergyText), () => "能量不足!");
            TargetNoEnergyText = this.GetLocalization(nameof(TargetNoEnergyText), () => "目的站点缺乏待机电力!");
            UnnamedText = this.GetLocalization(nameof(UnnamedText), () => "未命名站点 ({0}, {1})");
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
            Item.value = Item.buyPrice(0, 1, 20, 0);
            Item.rare = ItemRarityID.Orange;
            Item.color = Tint;
            Item.createTile = ModContent.TileType<TeleportStationTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient(ItemID.Sapphire, 5).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 12).
                AddIngredient(CWRID.Item_DubiousPlating, 10).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 8).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient(ItemID.Sapphire, 5).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 12).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }
}
