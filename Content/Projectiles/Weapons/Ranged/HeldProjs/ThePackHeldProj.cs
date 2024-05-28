using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ThePackHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ThePack";
        public override int targetCayItem => ModContent.ItemType<ThePack>();
        public override int targetCWRItem => ModContent.ItemType<ThePackEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 130;
            FireTime = 28;
            HandDistance = 12;
            HandDistanceY = 5;
            HandFireDistance = 12;
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
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 5);
        }

        public override void HanderPlaySound() {
            SoundEngine.PlaySound(ScorchedEarth.ShootSound 
                with { Volume = 0.6f, Pitch = 0.2f, PitchRange = (-0.1f, 0.1f) }, Projectile.Center);
        }

        public override void FiringShoot() {
            
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
