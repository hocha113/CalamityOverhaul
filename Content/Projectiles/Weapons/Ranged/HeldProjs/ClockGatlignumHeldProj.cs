using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ClockGatlignumHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ClockGatlignum";
        public override int targetCayItem => ModContent.ItemType<ClockGatlignum>();
        public override int targetCWRItem => ModContent.ItemType<ClockGatlignumEcType>();
        public override void SetRangedProperty() {
            kreloadMaxTime = 100;
            FireTime = 8;
            HandDistance = 18;
            HandDistanceY = 0;
            HandFireDistance = 18;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = false;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.9f;
            RangeOfStress = 25;
            SpwanGunDustMngsData.splNum = 0.6f;
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ProjectileID.BulletHighVelocity;
            }
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Source, GunShootPos
                    , ShootVelocity.RotatedByRandom(0.06f) * Main.rand.NextFloat(0.9f, 1.2f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }
    }
}
