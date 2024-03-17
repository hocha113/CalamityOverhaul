using CalamityOverhaul.Common;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class CursedCapperHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CursedCapper";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.CursedCapper>();
        public override int targetCWRItem => ModContent.ItemType<CursedCapperEcType>();
        public override void SetRangedProperty() {
            ControlForce = 0.1f;
            GunPressure = 0.2f;
            Recoil = 2;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -8;
        }

        public override void FiringIncident() {
            base.FiringIncident();
        }

        public override void FiringShoot() {
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity
                    , ProjectileID.CursedBullet, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            CaseEjection();
            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
        }
    }
}
