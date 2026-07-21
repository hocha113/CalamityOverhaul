using CalamityOverhaul.Content.HackTimes.Scannables;

namespace CalamityOverhaul.Content.HackTimes.Targets
{
    /// <summary>液体目标，无实体块时兜底</summary>
    internal class WaterTargetType : HackTargetType
    {
        public override HackTargetKind Kind => HackTargetKind.Water;

        public override int HoverPriority => -10;

        public override IHackTarget TryDetectHovered(Vector2 mouseWorld) {
            if (!WaterScannable.TryGetScannableLiquid(mouseWorld, out int tx, out int ty)) return null;
            return new WaterScannable(tx, ty);
        }
    }
}
