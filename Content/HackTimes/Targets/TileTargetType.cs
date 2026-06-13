using CalamityOverhaul.Content.HackTimes.Scannables;

namespace CalamityOverhaul.Content.HackTimes.Targets
{
    /// <summary>物块目标工厂，兜底最低优先级</summary>
    internal class TileTargetType : HackTargetType
    {
        public override HackTargetKind Kind => HackTargetKind.Tile;

        public override int HoverPriority => 0;

        public override IHackTarget TryDetectHovered(Vector2 mouseWorld) {
            if (!TileScannable.TryGetScannableTile(mouseWorld, out int tx, out int ty)) return null;
            return new TileScannable(tx, ty);
        }
    }
}
