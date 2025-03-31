using CalamityOverhaul.Content.TileModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    internal class CWRTile : GlobalTile
    {
        public override bool CanDrop(int i, int j, int type) {
            bool? reset = null;
            if (TileModifyLoader.TileOverrideDic.TryGetValue(type, out var rTile)) {
                reset = rTile.CanDrop(i, j, type);
            }
            return reset ?? base.CanDrop(i, j, type);
        }

        public override void MouseOver(int i, int j, int type) {
            if (TileModifyLoader.TileOverrideDic.TryGetValue(type, out var rTile)) {
                rTile.MouseOver(i, j);
            }
        }

        public override bool PreDraw(int i, int j, int type, SpriteBatch spriteBatch) {
            bool? reset = null;
            if (TileModifyLoader.TileOverrideDic.TryGetValue(type, out var rTile)) {
                reset = rTile.PreDraw(i, j, type, spriteBatch);
            }
            return reset ?? base.PreDraw(i, j, type, spriteBatch);
        }
    }
}
