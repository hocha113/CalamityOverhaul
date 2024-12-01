using CalamityOverhaul.Content.Items.Materials;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Tiles
{
    internal class NeutronStarIngotTile : ModTile, ICWRLoader
    {
        public override string Texture => CWRConstant.Asset + "Tiles/" + "NeutronStarIngotTile";
        private static Asset<Texture2D> tileAsset;
        private static Asset<Texture2D> tileGlowAsset;
        void ICWRLoader.LoadAsset() {
            tileAsset = ModContent.Request<Texture2D>(Texture);
            tileGlowAsset = ModContent.Request<Texture2D>(Texture + "Glow");
        }
        void ICWRLoader.UnLoadData() {
            tileAsset = null;
            tileGlowAsset = null;
        }
        public override void SetStaticDefaults() {
            AddMapEntry(new Color(121, 89, 9), CWRUtils.SafeGetItemName<BlackMatterStick>());
            Main.tileShine[Type] = 1100;
            Main.tileSolid[Type] = true;
            Main.tileSolidTop[Type] = true;
            Main.tileFrameImportant[Type] = true;
            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            return false;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            Texture2D tex = tileAsset.Value;
            Texture2D glow = tileGlowAsset.Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Color.White;

            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                Color glowColor = Color.White * MathF.Abs(MathF.Sin(Main.GameUpdateCount * 0.04f));
                spriteBatch.Draw(glow, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , glowColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }

            return false;
        }
    }
}
