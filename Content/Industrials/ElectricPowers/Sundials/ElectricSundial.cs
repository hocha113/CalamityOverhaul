using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Sundials
{
    /// <summary>电动日晷,储满电力即可把时间快进到黎明;贴图复用投掷者,靠晨曦金色调区分</summary>
    internal class ElectricSundial : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/Thrower";

        /// <summary>系列色调:晨曦金</summary>
        internal static readonly Color Tint = new(255, 214, 120);

        public static LocalizedText NoEnergyText { get; private set; }
        public static LocalizedText BusyText { get; private set; }
        public static LocalizedText SkipBroadcast { get; private set; }

        public override void SetStaticDefaults() {
            NoEnergyText = this.GetLocalization(nameof(NoEnergyText), () => "能量不足!");
            BusyText = this.GetLocalization(nameof(BusyText), () => "时间已在快进中!");
            SkipBroadcast = this.GetLocalization(nameof(SkipBroadcast), () => "{0} 启动了电动日晷,时光飞逝,直至黎明");
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
            Item.value = Item.buyPrice(0, 1, 50, 0);
            Item.rare = ItemRarityID.Orange;
            Item.color = Tint;
            Item.createTile = ModContent.TileType<ElectricSundialTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 1000;
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient(ItemID.SunplateBlock, 10).
                AddRecipeGroup(CWRCrafted.GoldBarGroup, 8).
                AddIngredient(CWRID.Item_DubiousPlating, 10).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 10).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient(ItemID.SunplateBlock, 10).
                AddRecipeGroup(CWRCrafted.GoldBarGroup, 8).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }
}
