using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Tiles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class InfiniteToiletItem : ModItem
    {
        public override string Texture => CWRConstant.Item + "Placeable/" + "InfiniteToiletItem";
        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 20;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<InfiniteIngotTile>();
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset) {
            if (line.Name == "ItemName" && line.Mod == "Terraria") {
                Vector2 basePosition = Main.MouseWorld - Main.screenPosition + new Vector2(23, 23);
                string text = Language.GetTextValue("Mods.CalamityOverhaul.Items.InfiniteToiletItem.DisplayName");
                InfiniteIngot.drawColorText(Main.spriteBatch, line, text, basePosition);
                return false;
            }
            return true;
        }

        public static void DrawItemIcon(SpriteBatch spriteBatch, Vector2 position, int Type, float alp = 1) {
            spriteBatch.Draw(TextureAssets.Item[Type].Value, position, null, Color.White, 0, TextureAssets.Item[Type].Value.Size() / 2, 0.8f, SpriteEffects.None, 0);
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            return true;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            spriteBatch.Draw(TextureAssets.Item[Type].Value, Item.Center - Main.screenPosition, null, Main.DiscoColor, 0, TextureAssets.Item[Type].Value.Size() / 2, 1, SpriteEffects.None, 0);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<InfiniteIngot>(15)
                .AddBlockingSynthesisEvent()
                .Register();
        }
    }
}
