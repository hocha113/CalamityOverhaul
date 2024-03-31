using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class ChlorophyteShotbowHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.ChlorophyteShotbow].Value;
        public override int targetCayItem => ItemID.ChlorophyteShotbow;
        public override int targetCWRItem => ItemID.ChlorophyteShotbow;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.WoodenArrowFriendly) {
                AmmoTypes = ProjectileID.ChlorophyteArrow;
            }
            _ = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            for (int i = 0; i < 3; i++) {
                _ = Projectile.NewProjectile(Source2, GunShootPos, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.07f, 0.07f)) * Main.rand.NextFloat(0.8f, 1f), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }
    }
}
