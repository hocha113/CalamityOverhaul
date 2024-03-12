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
            var value = Owner.GetShootState().UseAmmoItemType;
            if (value == ItemID.WoodenArrow) {
                AmmoTypes = ProjectileID.ChlorophyteArrow;
            }
            _ = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            for (int i = 0; i < 4; i++) {
                _ = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.07f, 0.07f)) * Main.rand.NextFloat(0.8f, 1f), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            _ = UpdateConsumeAmmo();
        }
    }
}
