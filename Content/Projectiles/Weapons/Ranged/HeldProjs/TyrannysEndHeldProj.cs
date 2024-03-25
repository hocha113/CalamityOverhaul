using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class TyrannysEndHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TyrannysEnd";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.TyrannysEnd>();
        public override int targetCWRItem => ModContent.ItemType<TyrannysEndEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 120;
            FireTime = 30;
            HandDistance = 45;
            HandDistanceY = 5;
            HandFireDistance = 45;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 8;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 6;
            RangeOfStress = 25;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 23);
            FireTime = MagazineSystem ? 30 : 50;
        }

        public override void FiringShoot() {
            SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            SpawnGunFireDust();
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, ModContent.ProjectileType<BMGBullet>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
            EjectCasing();
        }
    }
}
