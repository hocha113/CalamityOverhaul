using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RangedModify.Core;

namespace CalamityOverhaul.Content.RangedModify
{
    internal class GlobalGun : GlobalRanged
    {
        public override bool? CanUpdateMagazine(BaseFeederGun baseFeederGun) {
            if (CWRServerConfig.Instance.MagazineSystem
                && baseFeederGun.CalOwner.adrenalineModeActive) {//在肾上腺素下不会消耗弹匣子弹
                return false;
            }
            return base.CanUpdateMagazine(baseFeederGun);
        }
    }
}
