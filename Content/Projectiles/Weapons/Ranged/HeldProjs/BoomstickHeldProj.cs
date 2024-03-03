using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BoomstickHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Boomstick].Value;
        public override int targetCayItem => ItemID.Boomstick;
        public override int targetCWRItem => ItemID.Boomstick;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.8f;
            ControlForce = 0.05f;
            Recoil = 4.8f;
            RangeOfStress = 48;
        }

        public override void FiringShoot() {
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.12f, 0.12f)) * Main.rand.NextFloat(0.6f, 1.52f) * 0.3f, AmmoTypes, WeaponDamage, WeaponKnockback * 1.5f, Owner.whoAmI, 0); 
                _ = CreateRecoil();
            }
            //_ = UpdateConsumeAmmo();
        }
    }
}
