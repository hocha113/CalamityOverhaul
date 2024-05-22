using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class PlagueTaintedSMGHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "PlagueTaintedSMG";
        public override int targetCayItem => ModContent.ItemType<PlagueTaintedSMG>();
        public override int targetCWRItem => ModContent.ItemType<PlagueTaintedSMGEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 6;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            GunPressure = 0.06f;
            ControlForce = 0.03f;
            Recoil = 0.2f;
            RangeOfStress = 25;
            CanRightClick = true;
            FiringDefaultSound = false;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
            CanRightClick = BulletNum >= 6;
            if (CalOwner.mouseRight && BulletNum < 6) {
                SetAutomaticCartridgeChange(true);
            }
        }

        public override void FiringShoot() {
            FireTime = 6;
            GunPressure = 0.1f;
            Recoil = 0.2f;
            SpawnGunFireDust();
            SoundEngine.PlaySound(CWRSound.Gun_SMG_Shoot with { Pitch = -0.2f, Volume = 0.35f }, Projectile.Center);
            OffsetPos -= ShootVelocity.UnitVector() * 4;
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, ModContent.ProjectileType<PlagueTaintedProjectile>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void FiringShootR() {
            FireTime = 55;
            GunPressure = 0.5f;
            Recoil = 1.2f;
            SoundEngine.PlaySound(SoundID.Item61, Projectile.Center);
            OffsetPos -= ShootVelocity.UnitVector() * 6;
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Source2, GunShootPos, ShootVelocity.RotatedBy(-0.15f * (i + 1))
                    , ModContent.ProjectileType<PlagueTaintedDrone>(), WeaponDamage, WeaponKnockback
                    , Owner.whoAmI, 1f, Owner.Calamity().alchFlask || Owner.Calamity().spiritOrigin ? 1f : 0f);
                Projectile.NewProjectile(Source2, GunShootPos, ShootVelocity.RotatedBy(0.15f * (i + 1))
                    , ModContent.ProjectileType<PlagueTaintedDrone>(), WeaponDamage, WeaponKnockback
                    , Owner.whoAmI, 1f, Owner.Calamity().alchFlask || Owner.Calamity().spiritOrigin ? 1f : 0f);
            }
            for (int i = 0; i < 5; i++) {
                UpdateMagazineContents();
            }
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
            CaseEjection();
        }
    }
}
