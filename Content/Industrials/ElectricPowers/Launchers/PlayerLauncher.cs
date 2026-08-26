using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Launchers
{
    /// <summary>弹射平台,把站上来的玩家按设定方向抛出去;贴图复用投掷者,靠电蓝色调区分</summary>
    internal class PlayerLauncher : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/PlayerLauncher";

        /// <summary>系列色调:电蓝,用于提示文本与方向箭头点缀</summary>
        internal static readonly Color Tint = new(140, 210, 255);

        public static LocalizedText NoEnergyText { get; private set; }

        public override void SetStaticDefaults() {
            NoEnergyText = this.GetLocalization(nameof(NoEnergyText), () => "能量不足!");
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
            Item.value = Item.buyPrice(0, 1, 0, 0);
            Item.rare = ItemRarityID.Orange;
            Item.createTile = ModContent.TileType<PlayerLauncherTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 500;
        }

        public override void AddRecipes() {
                CreateRecipe()
                .AddIngredient<CircuitBoard>(6)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 10)
                .AddIngredient(ItemID.PinkGel, 20)
                .AddTile(TileID.Anvils)
                .Register();

        }
    }
}
