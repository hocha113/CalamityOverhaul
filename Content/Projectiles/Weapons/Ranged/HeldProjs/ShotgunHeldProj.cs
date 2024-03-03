using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ShotgunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Shotgun].Value;
        public override int targetCayItem => ItemID.Shotgun;
        public override int targetCWRItem => ItemID.Shotgun;
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
            for (int i = 0; i < 4; i++) {
                Projectile.NewProjectile(Owner.parent(), Projectile.Center
                    , ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.13f, 0.13f)) * Main.rand.NextFloat(0.75f, 1.05f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
        }
    }
}
