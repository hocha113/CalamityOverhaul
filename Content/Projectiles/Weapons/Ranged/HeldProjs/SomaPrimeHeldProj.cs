using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles;
using CalamityOverhaul.Content.RangedModify.Core;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SomaPrimeHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SomaPrime";
        public override int TargetID => ModContent.ItemType<SomaPrime>();
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 2;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.2f;
            RangeOfStress = 25;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            RecoilRetroForceMagnitude = 3;
            SpwanGunDustMngsData.splNum = 0.5f;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = false;
            EnableRecoilRetroEffect = true;
            SpwanGunDustMngsData.dustID1 = DustID.Adamantite;
            SpwanGunDustMngsData.dustID2 = DustID.SailfishBoots;
            SpwanGunDustMngsData.dustID3 = DustID.UltraBrightTorch;
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ProjectileID.BulletHighVelocity;
                WeaponDamage += 14;
            }
            int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            CalamityGlobalProjectile cgp = Main.projectile[proj].Calamity();
            cgp.supercritHits = -1;
            cgp.appliesSomaShred = true;
            float value = MathF.Sin(Time * 0.05f) * 0.3f;
            Vector2 newPos = ShootPos - ShootVelocity.UnitVector() * 46;
            int proj2 = Projectile.NewProjectile(Source, newPos, ShootVelocity.RotatedBy(value), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            CalamityGlobalProjectile cgp2 = Main.projectile[proj2].Calamity();
            cgp2.supercritHits = -1;
            cgp2.appliesSomaShred = true;
            int proj3 = Projectile.NewProjectile(Source, newPos, ShootVelocity.RotatedBy(-value), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            CalamityGlobalProjectile cgp3 = Main.projectile[proj3].Calamity();
            cgp3.supercritHits = -1;
            cgp3.appliesSomaShred = true;
        }
    }
}
