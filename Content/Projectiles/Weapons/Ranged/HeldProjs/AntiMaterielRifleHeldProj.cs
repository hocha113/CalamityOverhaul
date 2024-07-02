using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AntiMaterielRifleHeldProj : TyrannysEndHeldProj
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AntiMaterielRifle";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.AntiMaterielRifle>();
        public override int targetCWRItem => ModContent.ItemType<AntiMaterielRifleEcType>();
        public override void SetRangedProperty() {
            kreloadMaxTime = 120;
            FireTime = 60;
            ControlForce = 0.04f;
            GunPressure = 0.25f;
            Recoil = 3.5f;
            HandDistance = 35;
            HandFireDistance = 30;
            HandFireDistanceY = -8;
            ShootPosToMouLengValue = 30;
            ShootPosNorlLengValue = -5;
            RangeOfStress = 25;
            SpwanGunDustMngsData.splNum = 3.3f;
        }
        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                    , ModContent.ProjectileType<BMGBullet>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 1);
        }
    }
}
