using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic.Extras;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class RainbowMarshmallowsHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Item_Magic + "Marshmallow";
        public override int targetCayItem => ModContent.ItemType<RainbowMarshmallows>();
        public override int targetCWRItem => ModContent.ItemType<RainbowMarshmallows>();
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 60;
            ShootPosNorlLengValue = -20;
            HandDistance = 0;
            HandDistanceY = 0;
            HandFireDistance = 0;
            HandFireDistanceY = 0;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
        }

        public override void FiringIncident() {
            base.FiringIncident();
        }

        public override int Shoot() {
            int proj = 0;
            for (int i = 0; i < 3; i++) {
                if (Owner.CheckMana(Item)) {
                    proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.3f) * Main.rand.NextFloat(0.3f, 1.1f)
                        , Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    Owner.statMana -= Item.mana;
                }
            }
            return proj;
        }

        public override void GunDraw(ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, null, onFire ? Color.White : lightColor
                , Projectile.rotation, DirSign > 0 ? new Vector2(20, 120) : new Vector2(20, 20), Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
