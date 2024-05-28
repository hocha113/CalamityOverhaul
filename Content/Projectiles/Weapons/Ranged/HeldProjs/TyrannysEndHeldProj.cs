using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class TyrannysEndHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TyrannysEnd";
        public override int targetCayItem => ModContent.ItemType<TyrannysEnd>();
        public override int targetCWRItem => ModContent.ItemType<TyrannysEndEcType>();

        public override void SetRangedProperty() {
            Recoil = 6;
            FireTime = 20;
            kreloadMaxTime = 120;
            RangeOfStress = 25;
            HandDistance = 45;
            HandDistanceY = 5;
            GunPressure = 0.3f;
            ControlForce = 0.02f;
            HandFireDistance = 45;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = true;
            AutomaticPolishingEffect = true;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 23);
            FireTime = MagazineSystem ? 30 : 50;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , ModContent.ProjectileType<BMGBullet>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
