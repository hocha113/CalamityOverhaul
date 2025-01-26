using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SpykerHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Spyker";
        public override int targetCayItem => ModContent.ItemType<Spyker>();
        public override int targetCWRItem => ModContent.ItemType<SpykerEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 12;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -6;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            EjectCasingProjSize = 1.6f;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ModContent.ProjectileType<SpykerProj>();
            }
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
