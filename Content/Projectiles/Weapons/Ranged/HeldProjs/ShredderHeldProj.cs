using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ShredderHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Shredder";
        public override int targetCayItem => ModContent.ItemType<Shredder>();
        public override int targetCWRItem => ModContent.ItemType<ShredderEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 5;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -2;
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

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
        }

        public override void PostInOwnerUpdate() {
            base.PostInOwnerUpdate();
        }

        public override void FiringShoot() {
            FireTime = 5;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            RecoilRetroForceMagnitude = 4;
            Recoil = 0.3f;
            for (int index = 0; index < 4; ++index) {
                float SpeedX = ShootVelocity.X + Main.rand.Next(-30, 31) * 0.05f;
                float SpeedY = ShootVelocity.Y + Main.rand.Next(-30, 31) * 0.05f;
                int shredderBoltDamage = (int)(0.85f * WeaponDamage);
                int shot = Projectile.NewProjectile(Source, GunShootPos, new Vector2(SpeedX, SpeedY), ModContent.ProjectileType<ChargedBlast>(), shredderBoltDamage, WeaponKnockback, Owner.whoAmI, 0f, 0f);
                Main.projectile[shot].timeLeft = 180;
            }
        }

        public override void FiringShootR() {
            FireTime = 20;
            GunPressure = 0;
            ControlForce = 0;
            RecoilRetroForceMagnitude = 14;
            Recoil = 2.3f;
            for (int index = 0; index < 26; ++index) {
                float SpeedX = ShootVelocity.X + Main.rand.Next(-30, 31) * 0.05f;
                float SpeedY = ShootVelocity.Y + Main.rand.Next(-30, 31) * 0.05f;
                int shredderBoltDamage = (int)(0.7f * WeaponDamage);
                int shot = Projectile.NewProjectile(Source, GunShootPos, new Vector2(SpeedX, SpeedY), AmmoTypes, shredderBoltDamage, WeaponKnockback, Owner.whoAmI, 0f, 0f);
                Main.projectile[shot].timeLeft = 120;
                Main.projectile[shot].MaxUpdates *= 2;
            }
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
        }
    }
}
