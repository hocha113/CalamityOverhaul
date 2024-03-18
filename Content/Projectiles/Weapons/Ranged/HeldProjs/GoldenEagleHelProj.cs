using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class GoldenEagleHelProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "GoldenEagle";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.GoldenEagle>();
        public override int targetCWRItem => ModContent.ItemType<GoldenEagleEcType>();
        public override void SetRangedProperty() {
            HandDistance = 18;
            HandFireDistance = 18;
            HandFireDistanceY = -6;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            CanRightClick = true;
        }

        public override void FiringIncident() {
            base.FiringIncident();
            if (onFireR) {
                RangeOfStress = 25;
                GunPressure = 0.8f;
                ControlForce = 0.07f;
            }
            else {
                RangeOfStress = 5;
                GunPressure = 0.2f;
                ControlForce = 0.05f;
            }
        }

        public override void FiringShoot() {
            base.FiringShoot();
            const float spread = 0.0425f;
            for (int o = 0; o < 5; o++) {
                CaseEjection();
            }
            for (int i = 0; i < 2; i++) {
                Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity.RotatedBy(-spread * (i + 1)), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI);
                Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity.RotatedBy(spread * (i + 1)), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI);
            }
        }

        public override void FiringShootR() {
            base.FiringShootR();
            CaseEjection();

            Vector2 shoot2Vr = ShootVelocity.GetNormalVector();
            
            float needsengsValue = Projectile.Center.Distance(Main.MouseWorld);
            Vector2 toMouVr = ShootVelocity.UnitVector() * needsengsValue + Projectile.Center;

            for (int i = 0; i < 7; i++) {
                Vector2 pos2 = shoot2Vr.RotatedBy((-3 + i) * 0.15f) * 1230 + toMouVr;
                Vector2 vr = pos2.To(Main.MouseWorld);
                Projectile.NewProjectile(Owner.parent(), pos2, vr.UnitVector() * 13
                    , AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI);
            }
            for (int i = 0; i < 7; i++) {
                Vector2 pos4 = shoot2Vr.RotatedBy((-3 + i) * 0.15f) * -1230 + toMouVr;
                Vector2 vr2 = pos4.To(Main.MouseWorld);
                Projectile.NewProjectile(Owner.parent(), pos4, vr2.UnitVector() * 13
                    , AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI);
            }
        }

        public override int SpanLuxirProj(int luxirDamage) {
            return base.SpanLuxirProj(luxirDamage);
        }
    }
}
