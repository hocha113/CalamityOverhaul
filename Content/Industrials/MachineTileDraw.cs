using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Industrials
{
    /// <summary>
    /// 机器瓦片通用单元绘制:标准帧采样(兼容底行 18px 的泰拉"底部+2"约定),
    /// 可选"断电减半"暗化——全系机器统一的缺电状态语言
    /// </summary>
    internal static class MachineTileDraw
    {
        /// <param name="i">物块 x</param>
        /// <param name="j">物块 y</param>
        /// <param name="spriteBatch">画笔</param>
        /// <param name="tileType">瓦片类型</param>
        /// <param name="rows">整机图格行数(用于判定底行)</param>
        /// <param name="bottomHeight">底行源高(16=无沉降,18/20=底部沉入地面)</param>
        /// <param name="dimmed">true 时机身减半暗化</param>
        internal static void DrawCell(int i, int j, SpriteBatch spriteBatch, int tileType, int rows, int bottomHeight, bool dimmed) {
            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            //帧内行号:行步距 16+2,底行源高可变
            int srcHeight = frameYPos / 18 % rows == rows - 1 ? bottomHeight : 16;
            Texture2D tex = TextureAssets.Tile[tileType].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);
            if (dimmed) {
                drawColor.R /= 2;
                drawColor.G /= 2;
                drawColor.B /= 2;
                drawColor.A = 255;
            }

            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, srcHeight)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, srcHeight)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
        }
    }
}
