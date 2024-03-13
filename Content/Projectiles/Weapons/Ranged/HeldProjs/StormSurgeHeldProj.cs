using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
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
            HandDistance = 15;
            HandDistanceY = 3;
            HandFireDistance = 15;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 10;
            GunPressure = 0.05f;
            ControlForce = 0.05f;
            Recoil = 1.5f;
            RangeOfStress = 25;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, ModContent.ProjectileType<StormSurgeTornado>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
