using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ClockGatlignumHeldProj : BaseHeldRanged
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ClockGatlignum";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.ClockGatlignum>();
        public override int targetCWRItem => ModContent.ItemType<ClockGatlignum>();
        private float offsetRot;
        private Vector2 offsetPos;
        public override void InOwner() {
            float armRotSengsFront = 60 * CWRUtils.atoR;
            float armRotSengsBack = 110 * CWRUtils.atoR;

            Projectile.Center = Owner.Center + new Vector2(DirSign * 15, 0);
            Projectile.rotation = DirSign > 0 ? MathHelper.ToRadians(20) : MathHelper.ToRadians(160);
            Projectile.timeLeft = 2;
            SetHeld();

            offsetRot -= 0.1f;
            if (offsetRot <= 0) {
                offsetRot = 0;
            }

            if (!Owner.mouseInterface) {
                if (Owner.PressKey()) {
                    Owner.direction = ToMouse.X > 0 ? 1 : -1;
                    Projectile.rotation = ToMouseA + offsetRot;
                    Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 20 + new Vector2(0, -3) + offsetPos;
                    armRotSengsBack = armRotSengsFront = (MathHelper.PiOver2 - (ToMouseA + offsetRot + 0.5f * DirSign)) * DirSign;
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

        private void SpawnGunDust(Vector2 pos, Vector2 velocity, int splNum = 1) {
            if (Main.myPlayer != Projectile.owner) return;

            pos += velocity.SafeNormalize(Vector2.Zero) * Projectile.width * Projectile.scale * 0.71f;
            for (int i = 0; i < 30 * splNum; i++) {
                int dustID;
                switch (Main.rand.Next(6)) {
                    case 0:
                        dustID = 262;
                        break;
                    case 1:
                    case 2:
                        dustID = 54;
                        break;
                    default:
                        dustID = 53;
                        break;
                }
                float num = Main.rand.NextFloat(3f, 13f) * splNum;
                float angleRandom = 0.06f;
                Vector2 dustVel = new Vector2(num, 0f).RotatedBy((double)velocity.ToRotation(), default);
                dustVel = dustVel.RotatedBy(0f - angleRandom);
                dustVel = dustVel.RotatedByRandom(2f * angleRandom);
                if (Main.rand.NextBool(4)) {
                    dustVel = Vector2.Lerp(dustVel, -Vector2.UnitY * dustVel.Length(), Main.rand.NextFloat(0.6f, 0.85f)) * 0.9f;
                }
                float scale = Main.rand.NextFloat(0.5f, 1.5f);
                int idx = Dust.NewDust(pos, 1, 1, dustID, dustVel.X, dustVel.Y, 0, default, scale);
                Main.dust[idx].noGravity = true;
                Main.dust[idx].position = pos;
            }
        }

        public override void SpanProj() {
            if (onFire && Projectile.ai[1] > heldItem.useTime) {
                SoundEngine.PlaySound(heldItem.UseSound, Projectile.Center);
                Vector2 gundir = Projectile.rotation.ToRotationVector2();

                if (Main.rand.NextBool())
                    SpawnGunDust(Projectile.Center, gundir);

                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(Owner.parent(), Projectile.Center
                        , gundir.RotatedBy(Main.rand.NextFloat(-0.12f, 0.12f)) * Main.rand.NextFloat(0.6f, 1.52f) * 13
                        , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                }
                
                offsetRot += 0.25f;
                Projectile.ai[1] = 0;

                if (Math.Abs(Owner.velocity.X) < 5) {
                    Owner.velocity += gundir * -1.3f;
                }

                onFire = false;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, onFire ? Color.White : lightColor
                , Projectile.rotation, value.Size() / 2, Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            return false;
        }
    }
}
