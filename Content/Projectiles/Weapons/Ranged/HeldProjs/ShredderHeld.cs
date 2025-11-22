using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ShredderHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Shredder";
        public override void SetRangedProperty() {
            KreloadMaxTime = 90;
            FireTime = 5;
            HandIdleDistanceX = 24;
            HandIdleDistanceY = 4;
            HandFireDistanceX = 24;
            HandFireDistanceY = -4;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.3f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 4;
            CanRightClick = true;
        }

        public override void SetShootAttribute() {
            if (onFire) {
                FireTime = 5;
                GunPressure = 0.1f;
                ControlForce = 0.05f;
                RecoilRetroForceMagnitude = 4;
                Recoil = 0.3f;
                EjectCasingProjSize = 1;
                SpwanGunDustData.splNum = 0.3f;
                SpwanGunDustData.dustID1 = DustID.FireworkFountain_Blue;
                SpwanGunDustData.dustID2 = DustID.FireworkFountain_Blue;
                SpwanGunDustData.dustID3 = DustID.FireworkFountain_Blue;
            }
            else if (onFireR) {
                FireTime = 20;
                GunPressure = 0;
                ControlForce = 0;
                RecoilRetroForceMagnitude = 14;
                Recoil = 2.3f;
                EjectCasingProjSize = 1.4f;
                SpwanGunDustData.splNum = 1.3f;
                SpwanGunDustData.dustID1 = 262;
                SpwanGunDustData.dustID2 = 54;
                SpwanGunDustData.dustID3 = 53;
            }
        }

        public override void FiringShoot() {
            for (int index = 0; index < 3; ++index) {
                float SpeedX = ShootVelocity.X + Main.rand.Next(-30, 31) * 0.05f;
                float SpeedY = ShootVelocity.Y + Main.rand.Next(-30, 31) * 0.05f;
                int shredderBoltDamage = (int)(0.85f * WeaponDamage);
                int shot = Projectile.NewProjectile(Source, ShootPos, new Vector2(SpeedX, SpeedY)
                    , CWRID.Proj_ChargedBlast, shredderBoltDamage, WeaponKnockback, Owner.whoAmI, 0f, 0f);
                Main.projectile[shot].timeLeft = 180;
            }
        }

        public override void FiringShootR() {
            for (int index = 0; index < 18; ++index) {
                float SpeedX = ShootVelocity.X + Main.rand.Next(-30, 31) * 0.05f;
                float SpeedY = ShootVelocity.Y + Main.rand.Next(-30, 31) * 0.05f;
                int shredderBoltDamage = (int)(0.7f * WeaponDamage);
                int shot = Projectile.NewProjectile(Source2, ShootPos, new Vector2(SpeedX, SpeedY)
                    , AmmoTypes, shredderBoltDamage, WeaponKnockback, Owner.whoAmI, 0f, 0f);
                Main.projectile[shot].timeLeft = 120;
                Main.projectile[shot].MaxUpdates *= 2;
            }
        }
    }
}
