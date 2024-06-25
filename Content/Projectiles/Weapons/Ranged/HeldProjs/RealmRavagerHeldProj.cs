using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class RealmRavagerHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "RealmRavager";
        public override int targetCayItem => ModContent.ItemType<RealmRavager>();
        public override int targetCWRItem => ModContent.ItemType<RealmRavagerEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 122;
            FireTime = 20;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 3.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 9;
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ModContent.ProjectileType<RealmRavagerBullet>();
            }
            for (int index = 0; index < 5; ++index) {
                Vector2 velocity = ShootVelocity;
                velocity.X += Main.rand.Next(-40, 41) * 0.05f;
                velocity.Y += Main.rand.Next(-40, 41) * 0.05f;
                Projectile.NewProjectile(Source, GunShootPos, velocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }
    }
}
