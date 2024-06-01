using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class UniversalGenesisHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "UniversalGenesis";
        public override int targetCayItem => ModContent.ItemType<UniversalGenesis>();
        public override int targetCWRItem => ModContent.ItemType<UniversalGenesisEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 120;
            FireTime = 18;
            HandDistance = 20;
            HandDistanceY = 0;
            HandFireDistance = 18;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -15;
            ShootPosToMouLengValue = 50;
            RepeatedCartridgeChange = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.5f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 12;
            LoadingAA_None.loadingAA_None_Roting = 50;
            LoadingAA_None.loadingAA_None_X = 15;
            LoadingAA_None.loadingAA_None_Y = 20;
        }

        public override void HanderSpwanDust() {
            SpawnGunFireDust(GunShootPos, ShootVelocity, 2, 173, 173, 173);
        }

        public override void FiringShoot() {
            for (int i = 0; i < 5; i++) {
                Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy((-2 + i) * 0.03f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            Vector2 pos = GunShootPos + new Vector2(Owner.Center.To(Main.MouseWorld).X * 0.3f, -700);
            for (int i = 0; i < 7; i++) {
                pos += CWRUtils.randVr(134);
                Vector2 vr = pos.To(Main.MouseWorld).UnitVector() * ShootSpeedModeFactor * Main.rand.NextFloat(1.9f, 2.6f);
                Projectile.NewProjectile(Source, pos, vr, ModContent.ProjectileType<UniversalGenesisStar>(), WeaponDamage / 2, WeaponKnockback / 2, Owner.whoAmI, i, 1);
            }
        }
    }
}
