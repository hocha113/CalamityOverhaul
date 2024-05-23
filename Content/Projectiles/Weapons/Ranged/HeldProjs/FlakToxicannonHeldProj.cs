using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
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
            kreloadMaxTime = 90;
            FireTime = 10;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
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
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
        }

        public override void PostInOwnerUpdate() {
            if (onFire && kreloadTimeValue <= 0) {
                float minRot = MathHelper.ToRadians(50);
                float maxRot = MathHelper.ToRadians(130);
                Projectile.rotation = MathHelper.Clamp(ToMouseA + MathHelper.Pi, minRot, maxRot) - MathHelper.Pi;
                if (ToMouseA + MathHelper.Pi > MathHelper.ToRadians(270)) {
                    Projectile.rotation = minRot - MathHelper.Pi;
                }
                Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * HandFireDistance + OffsetPos;
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation + 0.5f * DirSign)) * DirSign;
                SetCompositeArm();
            }
        }

        public override void FiringShoot() {
            FireTime = 5;
            AmmoTypes = ModContent.ProjectileType<FlakToxicannonProjectile>();
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocityInProjRot, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, ToMouse.Length());
            CaseEjection(1.3f);
            if (++fireIndex > 13) {
                FireTime = 30;
                SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/Item/DudFire") with { Volume = 0.8f, Pitch = -0.7f, PitchVariance = 0.1f }, Projectile.Center);
                fireIndex = 0;
                return;
            }
            SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/Item/FlakKrakenShoot") { Pitch = 0.65f, Volume = 0.5f }, Projectile.Center);
        }
    }
}
