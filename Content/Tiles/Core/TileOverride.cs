using Terraria;

namespace CalamityOverhaul.Content.Tiles.Core
{
    internal class TileOverride
    {
        public virtual int TargetID => -1;
        public virtual bool CanLoad() => true;
        public virtual bool? RightClick(int i, int j, Tile tile) {
            return null;
        }
    }
}
