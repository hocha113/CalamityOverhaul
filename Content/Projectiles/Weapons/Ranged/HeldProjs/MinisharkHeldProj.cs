using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MinisharkHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Item[ItemID.Minishark].Value;//原版就存在的物品直接访问资源槽就行
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, onFire ? Color.White : lightColor
                , Projectile.rotation, value.Size() / 2, Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            return false;
        }

        public override int targetCayItem => ItemID.Minishark;
        public override int targetCWRItem => ItemID.Minishark;//这样的用法可能需要进行一定的考查，因为基类的设计并没有考虑到原版物品
        public override void SetRangedProperty() {
            ControlForce = 0.06f;
            GunPressure = 0.2f;
            Recoil = 1f;
            HandDistance = 15;
            HandFireDistance = 20;
            HandFireDistanceY = -3;
        }

        public override void FiringIncident() {
            base.FiringIncident();
        }

        public override void FiringShoot() {
            Vector2 gundir = Projectile.rotation.ToRotationVector2();
            Projectile.NewProjectile(Owner.parent(), Projectile.Center + gundir * 3, Projectile.rotation.ToRotationVector2() * ScaleFactor
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);

            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
        }
    }
}
