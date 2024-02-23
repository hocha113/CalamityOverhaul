﻿using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MegasharkHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Item[ItemID.Megashark].Value;//原版就存在的物品直接访问资源槽就行
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, onFire ? Color.White : lightColor
                , Projectile.rotation, value.Size() / 2, Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            return false;
        }

        public override int targetCayItem => ItemID.Megashark;
        public override int targetCWRItem => ItemID.Megashark;//这样的用法可能需要进行一定的考查，因为基类的设计并没有考虑到原版物品
        public override void SetRangedProperty() {
            ControlForce = 0.07f;
            GunPressure = 0.25f;
            Recoil = 1.3f;
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
