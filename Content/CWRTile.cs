using CalamityOverhaul.Content.Tiles.Core;
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
    }
}
