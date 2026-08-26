using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.PotionBeacons
{
    /// <summary>药剂弥散信标,存入增益药水并向范围内玩家持续弥散效果;贴图复用特斯拉塔,靠紫色调区分</summary>
    internal class PotionBeacon : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/PotionBeacon";

        /// <summary>系列色调:药雾紫,用于提示文本与弥散粒子</summary>
        internal static readonly Color Tint = new(215, 165, 255);

        public static LocalizedText NoEnergyText { get; private set; }

        public override void SetStaticDefaults() {
            NoEnergyText = this.GetLocalization(nameof(NoEnergyText), () => "能量不足!");
        }

        public override void SetDefaults() {
            Item.width = 30;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 2, 20, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.createTile = ModContent.TileType<PotionBeaconTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }

        public override void AddRecipes() {
                CreateRecipe()
                .AddIngredient<CircuitBoard>(8)
                .AddRecipeGroup(CWRCrafted.TungstenBarGroup, 10)
                .AddIngredient(ItemID.Bottle, 10)
                .AddTile(TileID.Anvils)
                .Register();

        }
    }
}
