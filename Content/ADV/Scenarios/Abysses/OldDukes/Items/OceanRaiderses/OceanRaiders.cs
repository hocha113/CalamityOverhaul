using CalamityMod.Items.Materials;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OceanRaiderses
{
    internal class OceanRaiders : ModItem
    {
        public override string Texture => CWRConstant.Item_Placeable + "OceanRaiders";
        [VaultLoaden(CWRConstant.Item_Placeable + "OceanRaidersGlow")]
        public static Asset<Texture2D> Glow = null;
        public static LocalizedText WorkingText { get; private set; }
        public static LocalizedText NoWaterText { get; private set; }
        public static LocalizedText NoEnergyText { get; private set; }
        
        public override void SetStaticDefaults() {
            WorkingText = this.GetLocalization(nameof(WorkingText), () => "正在工作中...");
            NoWaterText = this.GetLocalization(nameof(NoWaterText), () => "需要放置在水域上方才能工作！");
            NoEnergyText = this.GetLocalization(nameof(NoEnergyText), () => "能量不足！");
        }

        public override void SetDefaults() {
            Item.width = 164;
            Item.height = 96;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 5, 0, 0);
            Item.rare = ItemRarityID.LightPurple;
            Item.createTile = ModContent.TileType<OceanRaidersTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 1200;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition, null, Color.White
                , rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<DubiousPlating>(30)
                .AddIngredient<MysteriousCircuitry>(30)
                .AddIngredient(ItemID.Wood, 100)
                .AddIngredient(ItemID.FishingPotion, 5)
                .AddIngredient(ItemID.CratePotion, 5)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}
