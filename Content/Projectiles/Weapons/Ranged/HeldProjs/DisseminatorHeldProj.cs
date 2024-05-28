using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DisseminatorHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Disseminator";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Disseminator>();
        public override int targetCWRItem => ModContent.ItemType<DisseminatorEcType>();
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 16;
            ShootPosNorlLengValue = -13;
            ControlForce = 0.05f;
            GunPressure = 0.12f;
            Recoil = 1.2f;
            HandDistance = 20;
            HandDistanceY = 5;
            HandFireDistance = 20;
            HandFireDistanceY = -8;
            CanRightClick = true;//可以右键使用
        }

        public override void SetShootAttribute() {
            if (onFire) {
                GunPressure = 0.12f;
                Recoil = 0.35f;
                FireTime = 24;
            }
            else if (onFireR) {
                GunPressure = 0.1f;
                Recoil = 0.3f;
                FireTime = 4;
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 6; i++) {
                Projectile.NewProjectile(Source, GunShootPos,
                    ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f)) * Main.rand.NextFloat(0.8f, 1.1f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            Vector2 pos = Owner.Center + new Vector2(MathHelper.Lerp(Main.MouseWorld.To(Owner.Center).X, 0, 1.3f), 780);
            Vector2 vr = pos.To(Main.MouseWorld).UnitVector() * ShootSpeedModeFactor;
            for (int i = 0; i < 4; i++) {
                Vector2 vr2 = vr.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f)) * Main.rand.NextFloat(0.8f, 1.1f);
                int proj = Projectile.NewProjectile(Source2, pos, vr2, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].tileCollide = false;
                Main.projectile[proj].scale += Main.rand.NextFloat(0.5f);
                Main.projectile[proj].extraUpdates += 1;
            }
            _ = UpdateConsumeAmmo();
        }

        public override void FiringShootR() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Vector2 pos = Owner.Center + new Vector2(MathHelper.Lerp(Main.MouseWorld.To(Owner.Center).X, 0, 0.9f), -780);
            Vector2 vr = pos.To(Main.MouseWorld).UnitVector() * ShootSpeedModeFactor;
            Projectile.NewProjectile(Source2, pos, vr, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            _ = UpdateConsumeAmmo();
        }
    }
}
