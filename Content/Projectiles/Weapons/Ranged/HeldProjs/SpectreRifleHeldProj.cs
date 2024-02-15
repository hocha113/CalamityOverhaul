using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityMod.Projectiles.Rogue;
using CalamityMod.Particles;
using System;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SpectreRifleHeldProj : BaseHeldRanged
    {
        public override string Texture => CWRConstant.Item_Ranged + "SpectreRifle";
        private float offsetRot;
        public override bool CheckAlive() {
            return heldItem.type == ModContent.ItemType<SpectreRifle>();
        }

        public override void InOwner() {
            float armRotSengsFront = 60 * CWRUtils.atoR;
            float armRotSengsBack = 110 * CWRUtils.atoR;

            Projectile.Center = Owner.Center + new Vector2(DirSign * 30, 10);
            Projectile.rotation = DirSign > 0 ? MathHelper.ToRadians(20) : MathHelper.ToRadians(160);
            Projectile.timeLeft = 2;
            SetHeld();

            offsetRot -= 0.035f;
            if (offsetRot < 0) {
                offsetRot = 0;
            }

            if (!Owner.mouseInterface) {
                if (Owner.PressKey()) {
                    Owner.direction = ToMouse.X > 0 ? 1 : -1;
                    Projectile.rotation = ToMouseA - offsetRot * DirSign;
                    Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 25 + new Vector2(0, 0);
                    armRotSengsBack = armRotSengsFront = (MathHelper.PiOver2 - (ToMouseA + 0.5f * DirSign)) * DirSign;
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
                Vector2 pos = Projectile.Center + ShootVelocity.UnitVector() * 33 + ShootVelocity.GetNormalVector() * 5 * DirSign;
                for (int i = 0; i < 12; i++) {
                    int sparkLifetime = Main.rand.Next(22, 36);
                    float sparkScale = Main.rand.NextFloat(1f, 1.5f);
                    Color sparkColor = Color.WhiteSmoke;
                    Vector2 sparkVelocity = ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f)) * Main.rand.NextFloat(0.3f, 1.2f);
                    SparkParticle spark = new SparkParticle(pos, sparkVelocity, false, sparkLifetime, sparkScale, sparkColor);
                    GeneralParticleHandler.SpawnParticle(spark);
                }
                Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity
                        , ModContent.ProjectileType<LostSoulBullet>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                offsetRot += 0.75f;
                Projectile.ai[1] = 0;
                if (Math.Abs(Owner.velocity.X) < 16) {
                    Owner.velocity += ShootVelocity.UnitVector() * -5;
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
