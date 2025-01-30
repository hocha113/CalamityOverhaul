using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class GoldenEagleHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "GoldenEagle";
        public override int TargetID => ModContent.ItemType<GoldenEagle>();
        public override void SetRangedProperty() {
            kreloadMaxTime = 60;
            FireTime = 19;
            HandIdleDistanceX = 18;
            HandFireDistanceX = 18;
            HandFireDistanceY = -6;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            RepeatedCartridgeChange = true;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.gunBodyY = -10;
            Recoil = 0.8f;
        }

        public override void HanderCaseEjection() {
            if (onFire) {
                for (int o = 0; o < 5; o++) {
                    CaseEjection();
                }
            }
            else if (onFireR) {
                CaseEjection();
            }
        }

        public override void SetShootAttribute() {
            if (onFireR) {
                RangeOfStress = 25;
                GunPressure = 0.5f;
                ControlForce = 0.05f;
                Recoil = 1.8f;
                return;
            }
            RangeOfStress = 25;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 0.8f;
        }

        public override void FiringShoot() {

            base.FiringShoot();
            const float spread = 0.0425f;
            for (int i = 0; i < 2; i++) {
                Projectile.NewProjectile(Source2, Projectile.Center, ShootVelocity.RotatedBy(-spread * (i + 1)), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI);
                Projectile.NewProjectile(Source2, Projectile.Center, ShootVelocity.RotatedBy(spread * (i + 1)), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI);
            }
        }

        public override void FiringShootR() {

            base.FiringShootR();

            Vector2 shoot2Vr = ShootVelocity.GetNormalVector();

            float needsengsValue = Projectile.Center.Distance(Main.MouseWorld);
            Vector2 toMouVr = ShootVelocity.UnitVector() * needsengsValue + Projectile.Center;

            for (int i = 0; i < 7; i++) {
                Vector2 pos2 = shoot2Vr.RotatedBy((-3 + i) * 0.15f) * 1230 + toMouVr;
                Vector2 vr = pos2.To(Main.MouseWorld);
                Projectile.NewProjectile(Source2, pos2, vr.UnitVector() * 13
                    , AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI);
            }
            for (int i = 0; i < 7; i++) {
                Vector2 pos4 = shoot2Vr.RotatedBy((-3 + i) * 0.15f) * -1230 + toMouVr;
                Vector2 vr2 = pos4.To(Main.MouseWorld);
                Projectile.NewProjectile(Source2, pos4, vr2.UnitVector() * 13
                    , AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI);
            }
        }
    }
}
