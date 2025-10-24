﻿using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Tiles
{
    internal class InfiniteIngotTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "Tiles/" + "InfiniteIngotTile";
        [VaultLoaden(CWRConstant.Asset + "Tiles/" + "InfiniteIngotTile")]
        private static Asset<Texture2D> tileAsset = null;
        public override void SetStaticDefaults() {
            Main.tileShine[Type] = 1100;
            Main.tileSolid[Type] = true;
            Main.tileSolidTop[Type] = true;
            Main.tileFrameImportant[Type] = true;
            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);

            AddMapEntry(new Color(121, 89, 9), VaultUtils.GetLocalizedItemName<InfiniteIngot>());
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Vector2 pos = new Vector2(i, j) * 16 + new Vector2(Main.rand.Next(16), Main.rand.Next(16));
            PRT_SparkAlpha spark2 = new PRT_SparkAlpha(pos, new Vector2(0, Main.rand.Next(-3, 3)), true, 12, 0.4f, Main.DiscoColor);
            PRTLoader.AddParticle(spark2);
            return false;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            Texture2D tex = tileAsset.Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = VaultUtils.MultiStepColorLerp(Main.GameUpdateCount % 60 / 60f, HeavenfallLongbow.rainbowColors);

            if (!t.IsHalfBlock && t.Slope == 0)
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            else if (t.IsHalfBlock)
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            return false;
        }
    }
}
