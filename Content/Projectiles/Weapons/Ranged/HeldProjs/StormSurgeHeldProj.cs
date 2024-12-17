using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class StormSurgeHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "StormSurge";
        public override int targetCayItem => ModContent.ItemType<StormSurge>();
        public override int targetCWRItem => ModContent.ItemType<StormSurgeEcType>();

        public override void SetRangedProperty() {
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 3;
            HandFireDistanceX = 15;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 10;
            GunPressure = 0.05f;
            ControlForce = 0.05f;
            Recoil = 1.5f;
            RangeOfStress = 25;
            CanCreateCaseEjection = CanCreateSpawnGunDust = false;

        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , ModContent.ProjectileType<StormSurgeTornado>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
