using CalamityOverhaul.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class CursedCapperHeldProj : BaseFeederGun
    {
        public override bool IsLoadingEnabled(Mod mod) {
            return false;//TODO:这个项目已经废弃，等待移除或者重做为另一个目标的事项
        }

        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CursedCapper";
        //public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.CursedCapper>();
        //public override int targetCWRItem => ModContent.ItemType<CursedCapperEcType>();
        public override void SetRangedProperty() {
            FireTime = 8;
            ControlForce = 0.1f;
            GunPressure = 0.2f;
            Recoil = 1.2f;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -8;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.feederOffsetRot = -18;
            LoadingAA_Handgun.loadingAmmoStarg_x = 1;
            LoadingAA_Handgun.loadingAmmoStarg_y = -9;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, ProjectileID.CursedBullet, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            CaseEjection();
        }
    }
}
