using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class KarasawaHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Karasawa";
        public override int TargetID => ModContent.ItemType<Karasawa>();
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 30;
            HandIdleDistanceX = 30;
            HandIdleDistanceY = -5;
            HandFireDistanceX = 30;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = -30;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 2.2f;
            RangeOfStress = 25;
            if (!MagazineSystem) {
                FireTime += 8;
                LazyRotationUpdate = true;
            }
        }

        public override void HanderSpwanDust() {
            SpawnGunFireDust(dustID1: 187, dustID2: 229);
        }

        public override void FiringShoot() {
            OffsetPos -= ShootVelocity.UnitVector() * 18;
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
