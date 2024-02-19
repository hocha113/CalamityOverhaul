using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MinisharkHeldProj : BaseHeldGun
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
        public override float ControlForce => 0.06f;
        public override float GunPressure => 0.2f;
        public override float Recoil => 1.2f;
        public override void InOwner() {
            float armRotSengsFront = 60 * CWRUtils.atoR;
            float armRotSengsBack = 110 * CWRUtils.atoR;

            Projectile.Center = Owner.Center + new Vector2(DirSign * 15, 0);
            Projectile.rotation = DirSign > 0 ? MathHelper.ToRadians(20) : MathHelper.ToRadians(160);
            Projectile.timeLeft = 2;
            SetHeld();

            if (!Owner.mouseInterface) {
                if (Owner.PressKey()) {
                    Owner.direction = ToMouse.X > 0 ? 1 : -1;
                    Projectile.rotation = GunOnFireRot;
                    Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 20 + new Vector2(0, -3) + offsetPos;
                    armRotSengsBack = armRotSengsFront = (MathHelper.PiOver2 - Projectile.rotation) * DirSign;
                    if (HaveAmmo) {
                        onFire = true;
                        Projectile.ai[1]++;
                    }
                }
                else {
                    onFire = false;
                }
            }

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotSengsFront * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotSengsBack * -DirSign);
        }

        public override void SpanProj() {
            if (onFire && Projectile.ai[1] > heldItem.useTime) {
                SoundEngine.PlaySound(heldItem.UseSound, Projectile.Center);
                Vector2 gundir = Projectile.rotation.ToRotationVector2();
                Projectile.NewProjectile(Owner.parent(), Projectile.Center + gundir * 3, Projectile.rotation.ToRotationVector2() * ScaleFactor
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);

                _ = UpdateConsumeAmmo();
                _ = CreateRecoil();

                Projectile.ai[1] = 0;
                onFire = false;
            }
        }
    }
}
