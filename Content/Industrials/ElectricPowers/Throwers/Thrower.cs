using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Throwers
{
    /// <summary>
    /// 投掷者物品
    /// 可以自动将存储的物品投掷出去的机器
    /// </summary>
    internal class Thrower : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/Thrower";

        public static LocalizedText NoEnergyText { get; private set; }
        public static LocalizedText NoItemText { get; private set; }
        public static LocalizedText WorkingText { get; private set; }
        public static LocalizedText IdleText { get; private set; }

        [VaultLoaden(CWRConstant.UI + "SupertableUIs/InputArrow3")]
        internal static Asset<Texture2D> InputArrow;

        public override void SetStaticDefaults() {
            NoEnergyText = this.GetLocalization(nameof(NoEnergyText), () => "能量不足!");
            NoItemText = this.GetLocalization(nameof(NoItemText), () => "没有可投掷的物品!");
            WorkingText = this.GetLocalization(nameof(WorkingText), () => "投掷中...");
            IdleText = this.GetLocalization(nameof(IdleText), () => "待机");
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
            Item.createTile = ModContent.TileType<ThrowerTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 500;
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe()
                    .AddRecipeGroup(CWRRecipes.TinBarGroup, 10)
                    .AddIngredient(ItemID.IronBow, 1)
                    .AddTile(TileID.Anvils)
                    .Register();
                CreateRecipe()
                    .AddRecipeGroup(CWRRecipes.TinBarGroup, 10)
                    .AddIngredient(ItemID.LeadBow, 1)
                    .AddTile(TileID.Anvils)
                    .Register();
                return;
            }
            CreateRecipe()
                .AddIngredient(CWRID.Item_DubiousPlating, 8)
                .AddIngredient(CWRID.Item_MysteriousCircuitry, 6)
                .AddRecipeGroup(CWRRecipes.TinBarGroup, 10)
                .AddIngredient(ItemID.IronBow, 1)
                .AddTile(TileID.Anvils)
                .Register();
            CreateRecipe()
                .AddIngredient(CWRID.Item_DubiousPlating, 8)
                .AddIngredient(CWRID.Item_MysteriousCircuitry, 6)
                .AddRecipeGroup(CWRRecipes.TinBarGroup, 10)
                .AddIngredient(ItemID.LeadBow, 1)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
