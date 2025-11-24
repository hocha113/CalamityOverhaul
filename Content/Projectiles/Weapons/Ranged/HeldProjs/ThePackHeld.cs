using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ThePackHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ThePack";
        public override void SetRangedProperty() {
            KreloadMaxTime = 130;
            FireTime = 38;
            HandIdleDistanceX = 12;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 12;
            HandFireDistanceY = -6;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 2.2f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 16;
            LoadingAA_None.gunBodyX = 3;
            LoadingAA_None.gunBodyY = 10;
        }

        public override void HanderPlaySound() {
            SoundEngine.PlaySound("CalamityMod/Sounds/Item/ScorchedEarthShot3".GetSound()
                with { Volume = 0.6f, Pitch = 0.2f, PitchRange = (-0.1f, 0.1f) }, Projectile.Center);
        }

        public override void FiringShoot() {

            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
