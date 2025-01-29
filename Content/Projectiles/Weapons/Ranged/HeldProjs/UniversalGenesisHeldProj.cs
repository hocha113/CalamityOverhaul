using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class UniversalGenesisHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "UniversalGenesis";
        public override int TargetID => ModContent.ItemType<UniversalGenesis>();
        public override void SetRangedProperty() {
            kreloadMaxTime = 120;
            FireTime = 24;
            HandIdleDistanceX = 20;
            HandIdleDistanceY = 0;
            HandFireDistanceX = 18;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -4;
            ShootPosToMouLengValue = 50;
            RepeatedCartridgeChange = true;
            GunPressure = 0.1f;
            EjectCasingProjSize = 1.8f;
            ControlForce = 0.05f;
            Recoil = 0.5f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 12;
            LoadingAA_None.Roting = 50;
            LoadingAA_None.gunBodyX = 15;
            LoadingAA_None.gunBodyY = 20;
        }

        public override void HanderSpwanDust() {
            SpawnGunFireDust(ShootPos, ShootVelocity, 2, 173, 173, 173);
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ModContent.ProjectileType<UniversalGenesisStarcaller>();
            }

            for (int i = 0; i < 5; i++) {
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity.RotatedBy((-2 + i) * 0.03f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            Vector2 pos = ShootPos + new Vector2(Owner.Center.To(Main.MouseWorld).X * 0.3f, -700);
            for (int i = 0; i < 7; i++) {
                pos += CWRUtils.randVr(134);
                Vector2 vr = pos.To(Main.MouseWorld).UnitVector() * ShootSpeedModeFactor * Main.rand.NextFloat(1.9f, 2.6f);
                Projectile.NewProjectile(Source, pos, vr, ModContent.ProjectileType<UniversalGenesisStar>(), WeaponDamage / 2, WeaponKnockback / 2, Owner.whoAmI, i, 1);
            }
        }
    }
}
