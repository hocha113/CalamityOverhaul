using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AnimosityHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Animosity";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Animosity>();
        public override int targetCWRItem => ModContent.ItemType<Animosity>();
        public override void SetRangedProperty() {
            ControlForce = 0.1f;
            GunPressure = 0.2f;
            Recoil = 2;
            HandDistance = 27;
            HandDistanceY = 3;
            HandFireDistance = 27;
            HandFireDistanceY = -10;
            CanRightClick = true;
        }

        public override void FiringIncident() {
            base.FiringIncident();
            if (onFireR) {
                heldItem.useTime = 50;
                GunPressure = 0.6f;
                Recoil = 5;
                RangeOfStress = 25;
            }
            else {
                heldItem.useTime = 4;
                GunPressure = 0.2f;
                Recoil = 2;
                RangeOfStress = 5;
            }
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ProjectileID.BulletHighVelocity;
            }
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f))
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            CaseEjection();
            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
        }

        public override void FiringShootR() {
            Projectile.NewProjectile(Owner.parent(), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<AnimosityOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
            for (int i = 0; i < 9; i++) {
                CaseEjection();
                _ = UpdateConsumeAmmo();
            }
            _ = CreateRecoil();
        }
    }
}
