using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class FlakKrakenHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FlakKraken";
        public override int targetCayItem => ModContent.ItemType<FlakKraken>();
        public override int targetCWRItem => ModContent.ItemType<FlakKrakenEcType>();

        private int fireIndex;
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 10;
            HandDistance = 25;
            HandDistanceY = -5;
            HandFireDistance = 25;
            HandFireDistanceY = -15;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 1.2f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = true;
            FiringDefaultSound = false;
            RecoilRetroForceMagnitude = 17;
            RecoilOffsetRecoverValue = 0.8f;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.loadingAmmoStarg_y = -16;
        }

        public override void HanderCaseEjection() {
            CaseEjection(1.3f);
        }

        public override void SetShootAttribute() {
            FireTime = 8;
            RecoilRetroForceMagnitude = 17 + fireIndex;
            AmmoTypes = ModContent.ProjectileType<FlakKrakenProjectile>();
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocityInProjRot, AmmoTypes
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, ToMouse.Length());
        }

        public override void PostFiringShoot() {
            if (++fireIndex >= 5) {
                FireTime = 30;
                SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/Item/DudFire") 
                    with { Pitch = -0.7f, PitchVariance = 0.1f }, Projectile.Center);
                fireIndex = 0;
                return;
            }
            SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/Item/FlakKrakenShoot") 
            { Volume = 0.5f }, Projectile.Center);
        }
    }
}
