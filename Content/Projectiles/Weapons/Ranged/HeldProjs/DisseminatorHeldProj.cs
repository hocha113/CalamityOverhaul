using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DisseminatorHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Disseminator";
        public override int TargetID => ModContent.ItemType<Disseminator>();
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 16;
            ShootPosNorlLengValue = -4;
            ControlForce = 0.05f;
            GunPressure = 0.12f;
            Recoil = 1.2f;
            HandIdleDistanceX = 20;
            HandIdleDistanceY = 6;
            HandFireDistanceX = 20;
            HandFireDistanceY = 0;
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
                FireTime = 5;
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 5; i++) {
                Projectile.NewProjectile(Source, ShootPos,
                    ShootVelocity.RotateRandom(0.06f) * Main.rand.NextFloat(0.8f, 1.1f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            Vector2 pos = Owner.Center + new Vector2(MathHelper.Lerp(Main.MouseWorld.To(Owner.Center).X, 0, 1.3f), 780);
            NPC target = Main.MouseWorld.FindClosestNPC(1200);
            Vector2 targetPos = Main.MouseWorld;
            if (target != null) {
                targetPos = target.Center;
            }
            Vector2 vr = pos.To(targetPos).UnitVector() * ShootSpeedModeFactor;
            for (int i = 0; i < 8; i++) {
                Vector2 vr2 = vr.RotateRandom(0.06f) * Main.rand.NextFloat(0.8f, 1.1f);
                int proj = Projectile.NewProjectile(Source2, pos, vr2, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].tileCollide = false;
                Main.projectile[proj].scale += Main.rand.NextFloat(0.5f);
                Main.projectile[proj].extraUpdates += 1;
            }
            _ = UpdateConsumeAmmo();
        }

        public override void FiringShootR() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Vector2 pos = Owner.Center + new Vector2(MathHelper.Lerp(Main.MouseWorld.To(Owner.Center).X, 0, 0.9f), -780);
            Vector2 vr = pos.To(Main.MouseWorld).UnitVector() * ShootSpeedModeFactor;
            Projectile.NewProjectile(Source2, pos, vr, AmmoTypes, (int)(WeaponDamage * 0.7f), WeaponKnockback, Owner.whoAmI, 0);
            Vector2 pos2 = Owner.Center + new Vector2(MathHelper.Lerp(Main.MouseWorld.To(Owner.Center).X, 0, 0.9f), 780);
            Vector2 vr2 = pos2.To(Main.MouseWorld).UnitVector() * ShootSpeedModeFactor;
            Projectile.NewProjectile(Source2, pos2, vr2, AmmoTypes, (int)(WeaponDamage * 0.7f), WeaponKnockback, Owner.whoAmI, 0);
            _ = UpdateConsumeAmmo();
        }
    }
}
