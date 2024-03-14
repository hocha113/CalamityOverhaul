using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria;
using CalamityMod.Projectiles;
using CalamityMod;
using Terraria.ID;
using System;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SomaPrimeHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SomaPrime";
        public override int targetCayItem => ModContent.ItemType<SomaPrime>();
        public override int targetCWRItem => ModContent.ItemType<SomaPrimeEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 3;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 3;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
        }

        public override void PostInOwnerUpdate() {
            base.PostInOwnerUpdate();
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ProjectileID.BulletHighVelocity;
                WeaponDamage += 14;
            }
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            CalamityGlobalProjectile cgp = Main.projectile[proj].Calamity();
            cgp.supercritHits = -1;
            cgp.appliesSomaShred = true;
            float value = MathF.Sin(Time * 0.05f) * 0.3f;
            Vector2 newPos = GunShootPos - ShootVelocity.UnitVector() * 46;
            int proj2 = Projectile.NewProjectile(Source, newPos, ShootVelocity.RotatedBy(value), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            CalamityGlobalProjectile cgp2 = Main.projectile[proj2].Calamity();
            cgp2.supercritHits = -1;
            cgp2.appliesSomaShred = true;
            int proj3 = Projectile.NewProjectile(Source, newPos, ShootVelocity.RotatedBy(-value), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            CalamityGlobalProjectile cgp3 = Main.projectile[proj3].Calamity();
            cgp3.supercritHits = -1;
            cgp3.appliesSomaShred = true;
        }

        public override void FiringShootR() {
            base.FiringShootR();
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
        }
    }
}
