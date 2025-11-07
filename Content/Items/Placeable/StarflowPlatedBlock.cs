using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class StarflowPlatedBlock : ModItem
    {
        public override string Texture => CWRConstant.Item + "Placeable/StarflowPlatedBlock";

        public override void SetDefaults() {
            Item.width = 16;
            Item.height = 16;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 10;
            Item.useTime = 2;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(silver: 1);
            Item.rare = ItemRarityID.Blue;
            Item.tileBoost = 12;
            Item.createTile = ModContent.TileType<StarflowPlatedBlockTile>();
        }

        public override void AddRecipes() {
            CreateRecipe(10)
                .AddIngredient(CWRID.Item_ExoPrism, 80)
                .AddTile(CWRID.Tile_DraedonsForge)
                .Register();
        }
    }

    internal class StarflowPlatedBlockTile : ModTile
    {
        public override string Texture => CWRConstant.Item + "Placeable/StarflowPlatedBlock";

        public override void SetStaticDefaults() {
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = true;
            Main.tileLighted[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(150, 180, 220), VaultUtils.GetLocalizedItemName<StarflowPlatedBlock>());
            MineResist = 6f;
            MinPick = 125;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            r = 0.1f;
            g = 0.15f;
            b = 0.2f;
        }

        public override bool CreateDust(int i, int j, ref int type) {
            type = DustID.TreasureSparkle;
            return true;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile t = Main.tile[i, j];
            int frameXPos = 0;
            int frameYPos = 0;
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);
            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }
}
