using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class NeedlerHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Needler";
        public override int TargetID => ModContent.ItemType<Needler>();
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 12;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 15;
            HandFireDistanceY = -4;
            ShootPosNorlLengValue = -7;
            ShootPosToMouLengValue = 4;
            RepeatedCartridgeChange = true;
            CanCreateCaseEjection = false;
            Onehanded = InOwner_HandState_AlwaysSetInFireRoding = true;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
        }

        public override void HanderSpwanDust() {
            SpawnGunFireDust(dustID1: DustID.GreenTorch, dustID2: DustID.GreenMoss);
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = Item.shoot;
            }
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
