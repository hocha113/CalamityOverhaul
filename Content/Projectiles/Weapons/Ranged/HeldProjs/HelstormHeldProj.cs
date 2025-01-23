using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class HelstormHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Helstorm";
        public override int targetCayItem => ModContent.ItemType<Helstorm>();
        public override int targetCWRItem => ModContent.ItemType<HelstormEcType>();
        public override bool? CanDamage() => (onFire || onFireR) && IsKreload ? null : base.CanDamage();
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 360);
            HellbornHeldProj.HitFunc(Owner, target);
        }

        public override void SetRangedProperty() {
            FireTime = 20;
            ControlForce = 0.1f;
            GunPressure = 0.2f;
            Recoil = 1;
            HandIdleDistanceX = 27;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -8;
            CanRightClick = true;
        }

        public override void SetShootAttribute() {
            if (onFireR) {
                FireTime = 3;
                Recoil = 0.5f;
                GunPressure = 0.1f;
                EnableRecoilRetroEffect = true;
                EjectCasingProjSize = 1;
                SpwanGunDustMngsData.splNum = 0.4f;
                return;
            }
            FireTime = 20;
            Recoil = 1;
            GunPressure = 0.2f;
            EnableRecoilRetroEffect = false;
            EjectCasingProjSize = 1.6f;
            SpwanGunDustMngsData.splNum = 1f;
        }

        public override void FiringShoot() {
            for (int i = 0; i < 5; i++) {
                _ = Projectile.NewProjectile(Source, GunShootPos,
                    ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f)) * Main.rand.NextFloat(0.7f, 1.1f)
                    , ModContent.ProjectileType<RealmRavagerBullet>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }

        public override void FiringShootR() {
            _ = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.06f)
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
