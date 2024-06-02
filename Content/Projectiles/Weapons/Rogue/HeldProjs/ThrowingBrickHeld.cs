using CalamityMod.Projectiles.Rogue;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using System;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs
{
    internal class ThrowingBrickHeld : BaseThrowable
    {
        public override string Texture => CWRConstant.Cay_Wap_Rogue + "ThrowingBrick";
        private bool hitTheHead = false;
        public override void SetThrowable() {
            HandOnTwringMode = -20;
            OffsetRoting = 20 * CWRUtils.atoR;
            Projectile.penetrate = 1;
        }

        public override void FlyToMovementAI() {
            Projectile.rotation += 0.4f * Projectile.direction;
            Projectile.velocity.Y = Projectile.velocity.Y + 0.3f;
            if (Projectile.velocity.Y > 16f) {
                Projectile.velocity.Y = 16f;
            }
            if (Main.rand.NextBool(13)) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Pot
                    , Projectile.velocity.X * 0.25f, Projectile.velocity.Y * 0.25f, 150, default, 0.9f);
            }
            if (Math.Abs(Projectile.velocity.X) < 2 && ++Projectile.ai[2] > 30) {
                Projectile.hostile = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            return true;
        }

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers) {
            modifiers.SetMaxDamage(50);
            hitTheHead = true;
            Projectile.Kill();
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item50, Projectile.position);
            int dust_splash = 0;
            while (dust_splash < 9) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Copper
                    , -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f, 120, default, 1.5f);
                dust_splash += 1;
            }
            if (stealthStrike || hitTheHead) {
                int split = 0;
                while (split < 5) {
                    float shardspeedX = -Projectile.velocity.X * Main.rand.NextFloat(.1f, .15f) + Main.rand.NextFloat(-3f, 3f);
                    float shardspeedY = -Projectile.velocity.Y * Main.rand.NextFloat(.5f, .9f) + Main.rand.NextFloat(-6f, -3f);
                    if (shardspeedX < 1f && shardspeedX > -1f) {
                        shardspeedX += -Projectile.velocity.X;
                    }
                    if (shardspeedY > -3f) {
                        shardspeedY += -Projectile.velocity.Y;
                    }
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.position.X + shardspeedX
                        , Projectile.position.Y + shardspeedY, shardspeedX, shardspeedY
                        , ModContent.ProjectileType<BrickFragment>(), Projectile.damage / 2, Projectile.knockBack / 2f, Projectile.owner);
                    split += 1;
                }
            }
        }
    }
}
