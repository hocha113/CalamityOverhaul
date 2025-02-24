using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RangedModify
{
    internal class GlobalGun : GlobalRanged
    {
        public override void PreInOwnerByFeederGun(BaseFeederGun gun) {
            if (CWRServerConfig.Instance.MagazineSystem && gun.CalOwner.adrenalineModeActive) {
                gun.Owner.AddBuff(ModContent.BuffType<FrenziedMachineSoul>(), 10086);//For The Emperor!!!
            }
        }

        public override bool? CanUpdateMagazine(BaseFeederGun baseFeederGun) {
            if (CWRServerConfig.Instance.MagazineSystem
                && baseFeederGun.CalOwner.adrenalineModeActive) {//在肾上腺素下不会消耗弹匣子弹
                return false;
            }
            return base.CanUpdateMagazine(baseFeederGun);
        }
    }
}
