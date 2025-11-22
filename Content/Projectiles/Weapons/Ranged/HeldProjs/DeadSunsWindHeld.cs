using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DeadSunsWindHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "DeadSunsWind";
        public override void SetRangedProperty() {
            FireTime = 18;
            HandIdleDistanceX = 30;
            HandFireDistanceX = 30;
            HandFireDistanceY = -4;
            Recoil = 0;
            CanCreateCaseEjection = false;
            CanCreateSpawnGunDust = false;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity
                    , Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
