using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class FlakToxicannonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FlakToxicannon";
        public override int TargetID => ModContent.ItemType<FlakToxicannon>();
        public override void SetRangedProperty() {
            Recoil = 1.2f;
            FireTime = 20;
            GunPressure = 0;
            ControlForce = 0;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            RangeOfStress = 25;
            KreloadMaxTime = 90;
            HandFireDistanceX = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            RecoilRetroForceMagnitude = 17;
            FiringDefaultSound = false;
            CanCreateSpawnGunDust = false;
            RepeatedCartridgeChange = true;
            AmmoTypeAffectedByMagazine = false;
            HandheldDisplay = false;
            LazyRotationUpdate = true;
        }

        public override void HanderCaseEjection() {
            CaseEjection(1.3f);
        }

        public override void SetShootAttribute() {
            AmmoTypes = ModContent.ProjectileType<FlakToxicannonProjectile>();
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocityInProjRot
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, ToMouse.Length());
        }

        public override void PostFiringShoot() {
            FireTime = 20;
            if (++fireIndex >= 5) {
                FireTime = 40;
                SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/Item/DudFire")
                    with { Volume = 0.8f, Pitch = -0.7f, PitchVariance = 0.1f }, Projectile.Center);
                fireIndex = 0;
                return;
            }
            OffsetPos -= ShootVelocityInProjRot.UnitVector() * RecoilRetroForceMagnitude;
            SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/Item/FlakKrakenShoot") { Pitch = 0.65f, Volume = 0.3f }, Projectile.Center);
        }
    }
}
