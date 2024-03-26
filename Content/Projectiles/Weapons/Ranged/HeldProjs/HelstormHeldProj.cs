using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class HelstormHeldProj : BaseFeederGun
    {
        public override bool? CanDamage() {
            return (onFire || onFireR) && IsKreload ? null : base.CanDamage();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 360);
            HellbornHeldProj.HitFunc(Owner, target);
        }

        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Helstorm";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Helstorm>();
        public override int targetCWRItem => ModContent.ItemType<HelstormEcType>();

        public override void SetRangedProperty() {
            FireTime = 20;
            ControlForce = 0.1f;
            GunPressure = 0.2f;
            Recoil = 1;
            HandDistance = 27;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -8;
            CanRightClick = true;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
        }

        public override void FiringShoot() {
            SpawnGunFireDust();
            FireTime = 20;
            Recoil = 1;
            GunPressure = 0.2f;
            EnableRecoilRetroEffect = false;
            for (int i = 0; i < 5; i++) {
                _ = Projectile.NewProjectile(Source, GunShootPos,
                    ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f)) * Main.rand.NextFloat(0.7f, 1.1f)
                    , ModContent.ProjectileType<RealmRavagerBullet>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }

        public override void FiringShootR() {
            SpawnGunFireDust();
            FireTime = 3;
            Recoil = 0.5f;
            GunPressure = 0.1f;
            EnableRecoilRetroEffect = true;
            _ = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.06f), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
