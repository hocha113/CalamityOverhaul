using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AuralisHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Auralis";
        public override int TargetID => ModContent.ItemType<Auralis>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 32;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -0;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 13;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<AuralisBullet>();
            if (!MagazineSystem) {
                FireTime += 18;
                LazyRotationUpdate = true;
            }
        }
    }
}
