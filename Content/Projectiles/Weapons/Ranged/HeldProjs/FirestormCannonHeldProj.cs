using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class FirestormCannonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FirestormCannon";
        public override int targetCayItem => ModContent.ItemType<FirestormCannon>();
        public override int targetCWRItem => ModContent.ItemType<FirestormCannonEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 8;
            HandDistance = 15;
            HandDistanceY = 5;
            HandFireDistance = 15;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0.12f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            CanCreateCaseEjection = false;
            RecoilRetroForceMagnitude = 7;
            CanRightClick = true;
        }

        public override void SetShootAttribute() {
            if (onFireR) {
                FireTime = 30;
                GunPressure = 0.4f;
                Recoil = 2.2f;
                return;
            }
            FireTime = 8;
            GunPressure = 0.12f;
            Recoil = 1.2f;
        }

        public override void FiringShoot() {
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].timeLeft = 160;
        }

        public override void FiringShootR() {
            for (int i = 0; i < 5; i++) {
                int proj = Projectile.NewProjectile(Source, GunShootPos
                    , ShootVelocity.RotatedByRandom(0.1f) * Main.rand.NextFloat(1.3f, 1.5f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].timeLeft = 160;
            }
        }

        public override void PostInOwnerUpdate() {
            if (!IsKreload) {
                return;
            }

            int dustType = FlareGunHeldProj.GetFlareDustID(this);
            int dust = Dust.NewDust(GunShootPos, 1, 1, dustType, ShootVelocity.X, ShootVelocity.Y);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].scale = Main.rand.NextFloat(1, 1.6f);
        }
    }
}
