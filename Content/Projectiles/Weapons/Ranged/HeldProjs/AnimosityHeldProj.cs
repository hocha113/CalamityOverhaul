using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AnimosityHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Animosity";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Animosity>();
        public override int targetCWRItem => ModContent.ItemType<AnimosityEcType>();
        public override void SetRangedProperty() {
            FireTime = 6;
            ControlForce = 0.1f;
            GunPressure = 0.2f;
            Recoil = 2;
            HandDistance = 27;
            HandDistanceY = 3;
            HandFireDistance = 27;
            HandFireDistanceY = -10;
            CanRightClick = true;
        }

        public override void FiringShoot() {
            FireTime = 6;
            GunPressure = 0.2f;
            Recoil = 2;
            RangeOfStress = 5;
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ProjectileID.BulletHighVelocity;
            }
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f))
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            CaseEjection();
        }

        public override void FiringShootR() {
            FireTime = 50;
            GunPressure = 0.6f;
            Recoil = 5;
            RangeOfStress = 25;
            Projectile.NewProjectile(Source, Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<AnimosityOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
            for (int i = 0; i < 5; i++) {
                CaseEjection();
                UpdateMagazineContents();
            }
        }
    }
}
