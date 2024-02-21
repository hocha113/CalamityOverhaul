using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class QuadBarrelShotgunHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override int targetCayItem => ItemID.QuadBarrelShotgun;
        public override int targetCWRItem => ItemID.QuadBarrelShotgun;
        public override void SetRangedProperty() {
            ControlForce = 0.1f;
            GunPressure = 0.2f;
            Recoil = 2;
            HandDistance = 26;
            HandDistanceY = 3;
            HandFireDistance = 26;
            HandFireDistanceY = -9;
        }

        public override void FiringIncident() {
            base.FiringIncident();
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

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Item[ItemID.QuadBarrelShotgun].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, onFire ? Color.White : lightColor
                , Projectile.rotation, value.Size() / 2, Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            return false;
        }
    }
}
