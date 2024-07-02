using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class NullificationRifleHeldProj : BaseFeederGun
    {
        public override bool IsLoadingEnabled(Mod mod) {
            return false;//TODO:这个项目已经废弃，等待移除或者重做为另一个目标的事项
        }

        public override string Texture => CWRConstant.Cay_Wap_Ranged + "NullificationRifle";
        //public override int targetCayItem => ModContent.ItemType<NullificationRifle>();
        //public override int targetCWRItem => ModContent.ItemType<NullificationRifleEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 15;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 0;
            RepeatedCartridgeChange = true;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
        }

    }
}
