using CalamityOverhaul.Content.HackTimes.Scannables;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Targets
{
    /// <summary>
    /// 容器目标（箱子/梳妆台等 <c>Main.chest</c> 实体）。<br/>
    /// 优先级压在炮台(40)之下、物块(0)之上，悬停箱子时先认容器，
    /// 否则永远被物块目标截胡
    /// </summary>
    internal class ContainerTargetType : HackTargetType
    {
        public override HackTargetKind Kind => HackTargetKind.Container;

        public override int HoverPriority => 20;

        #region 扫描面板与锁定框文案（挂在目标种类上，键随 HackTargetType.ContainerTargetType.* 走）

        public static LocalizedText ScanType { get; private set; }
        public static LocalizedText ScanLock { get; private set; }
        public static LocalizedText ScanSlots { get; private set; }
        public static LocalizedText ScanValue { get; private set; }
        public static LocalizedText ScanTopRare { get; private set; }
        public static LocalizedText ScanTopValue { get; private set; }
        public static LocalizedText ScanCoord { get; private set; }
        public static LocalizedText ScanRest { get; private set; }
        public static LocalizedText ScanEmpty { get; private set; }
        public static LocalizedText LockStateLocked { get; private set; }
        public static LocalizedText LockStateOpen { get; private set; }
        public static LocalizedText StateIndexed { get; private set; }

        public override void SetStaticDefaults() {
            ScanType = this.GetLocalization(nameof(ScanType), () => "Container");
            ScanLock = this.GetLocalization(nameof(ScanLock), () => "Lock");
            ScanSlots = this.GetLocalization(nameof(ScanSlots), () => "Slots");
            ScanValue = this.GetLocalization(nameof(ScanValue), () => "Est. Value");
            ScanTopRare = this.GetLocalization(nameof(ScanTopRare), () => "Top Rarity");
            ScanTopValue = this.GetLocalization(nameof(ScanTopValue), () => "Most Valuable");
            ScanCoord = this.GetLocalization(nameof(ScanCoord), () => "Coordinates");
            ScanRest = this.GetLocalization(nameof(ScanRest), () => "...and {0} more entries");
            ScanEmpty = this.GetLocalization(nameof(ScanEmpty), () => "Empty");
            LockStateLocked = this.GetLocalization(nameof(LockStateLocked), () => "LOCKED");
            LockStateOpen = this.GetLocalization(nameof(LockStateOpen), () => "Unlocked");
            StateIndexed = this.GetLocalization(nameof(StateIndexed), () => "Index Cached");
        }

        #endregion

        public override IHackTarget TryDetectHovered(Vector2 mouseWorld) {
            if (!ContainerScannable.TryGetScannableContainer(mouseWorld,
                out int anchorX, out int anchorY)) {
                return null;
            }
            return new ContainerScannable(anchorX, anchorY);
        }
    }
}
