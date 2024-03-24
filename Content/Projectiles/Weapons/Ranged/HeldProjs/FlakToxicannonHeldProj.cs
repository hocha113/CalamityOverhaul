using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class FlakToxicannonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FlakToxicannon";
        public override int targetCayItem => ModContent.ItemType<FlakToxicannon>();
        public override int targetCWRItem => ModContent.ItemType<FlakToxicannonEcType>();

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
            RecoilRetroForceMagnitude = 7;
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
                Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * HandFireDistance;
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation + 0.5f * DirSign)) * DirSign;
                SetCompositeArm();
            }
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocityInProjRot, Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void FiringShootR() {
            base.FiringShootR();
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
        }
    }
}
