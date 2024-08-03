using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class FlakToxicannonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FlakToxicannon";
        public override int targetCayItem => ModContent.ItemType<FlakToxicannon>();
        public override int targetCWRItem => ModContent.ItemType<FlakToxicannonEcType>();

        private int fireIndex;
        public override void SetRangedProperty() {
            Recoil = 1.2f;
            FireTime = 10;
            GunPressure = 0;
            ControlForce = 0;
            HandDistance = 25;
            HandDistanceY = 5;
            RangeOfStress = 25;
            kreloadMaxTime = 90;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            RecoilRetroForceMagnitude = 17;
            FiringDefaultSound = false;
            CanCreateSpawnGunDust = false;
            RepeatedCartridgeChange = true;
            AmmoTypeAffectedByMagazine = false;
        }

        public override void PostInOwnerUpdate() {
            if (onFire && kreloadTimeValue <= 0) {
                float minRot = MathHelper.ToRadians(50);
                float maxRot = MathHelper.ToRadians(130);
                Projectile.rotation = MathHelper.Clamp(ToMouseA + MathHelper.Pi, minRot, maxRot) - MathHelper.Pi;
                if (ToMouseA + MathHelper.Pi > MathHelper.ToRadians(270)) {
                    Projectile.rotation = minRot - MathHelper.Pi;
                }
                Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.rotation.ToRotationVector2() * HandFireDistance + OffsetPos;
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation + 0.5f * DirSign)) * DirSign;
                SetCompositeArm();
            }
        }

        public override void HanderCaseEjection() {
            CaseEjection(1.3f);
        }

        public override void SetShootAttribute() {
            FireTime = 5;
            AmmoTypes = ModContent.ProjectileType<FlakToxicannonProjectile>();
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocityInProjRot
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, ToMouse.Length());
        }

        public override void PostFiringShoot() {
            if (++fireIndex >= 5) {
                FireTime = 60;
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
