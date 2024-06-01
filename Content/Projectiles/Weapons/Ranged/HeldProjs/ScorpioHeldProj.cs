using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;
using ScorchedEarth = CalamityMod.Items.Weapons.Ranged.ScorchedEarth;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ScorpioHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Scorpio";
        public override int targetCayItem => ModContent.ItemType<Scorpio>();
        public override int targetCWRItem => ModContent.ItemType<ScorpioEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 30;
            HandDistance = 22;
            HandDistanceY = 5;
            HandFireDistance = 22;
            HandFireDistanceY = -6;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 3.2f;
            RangeOfStress = 25;
            EjectCasingProjSize = 2;
            LoadingAA_None.loadingAA_None_Roting = -50;
            LoadingAA_None.loadingAA_None_X = 3;
            LoadingAA_None.loadingAA_None_Y = 0;
        }

        public override void PreInOwnerUpdate() {
            FireTime = MagazineSystem ? 30 : 75;
        }

        public override void FiringShoot() {
            ModOwner.SetScreenShake(4);
            SoundEngine.PlaySound(ScorchedEarth.ShootSound with { Pitch = 0.3f }, Projectile.Center);
            OffsetPos -= ShootVelocity.UnitVector() * 28;
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , ModContent.ProjectileType<ScorpioOnSpan>()
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI, 1);
        }
    }
}
