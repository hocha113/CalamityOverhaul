using CalamityMod;
using CalamityMod.Projectiles.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using static Humanizer.In;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic
{
    internal class GhastlyVisageBall : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Magic + "GhastlyVisageBall";
        public Vector2 SavedOldVelocity;
        public Vector2 NPCDestination;
        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.penetrate = 3;
            Projectile.MaxUpdates = 3;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 6);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
            Projectile.scale += 0.01f;
            Color color = Color.Lerp(Color.Red, Color.OrangeRed, Main.rand.NextFloat(0.3f, 0.64f));
            CWRParticle spark = new SparkParticle(Projectile.Center - Projectile.velocity * 8f, -Projectile.velocity * 0.1f, false, 19, 2.3f, color * 0.1f);
            CWRParticleHandler.AddParticle(spark);
            if (Projectile.scale > 1.15f) {
                Projectile.scale = 1.15f;
            }
            if (Projectile.timeLeft > 200) {
                Projectile.velocity.X *= 0.99f;
                Projectile.velocity.Y *= 0.995f;
            }
            else {
                CWRDust.SpanCycleDust(Projectile, DustID.RedTorch, DustID.RedTorch);
                for (int i = 0; i < Main.maxNPCs; i++) {
                    if (Main.npc[i].CanBeChasedBy(Projectile.GetSource_FromThis(), false))
                        NPCDestination = Main.npc[i].Center + Main.npc[i].velocity * 5f;
                }
                float returnSpeed = 10;
                float acceleration = 0.2f;
                float xDist = NPCDestination.X - Projectile.Center.X;
                float yDist = NPCDestination.Y - Projectile.Center.Y;
                float dist = (float)Math.Sqrt(xDist * xDist + yDist * yDist);
                dist = returnSpeed / dist;
                xDist *= dist;
                yDist *= dist;
                float targetDist = Vector2.Distance(NPCDestination, Projectile.Center);
                if (targetDist < 1800) {
                    if (Projectile.velocity.X < xDist) {
                        Projectile.velocity.X = Projectile.velocity.X + acceleration;
                        if (Projectile.velocity.X < 0f && xDist > 0f)
                            Projectile.velocity.X += acceleration;
                    } else if (Projectile.velocity.X > xDist) {
                        Projectile.velocity.X = Projectile.velocity.X - acceleration;
                        if (Projectile.velocity.X > 0f && xDist < 0f)
                            Projectile.velocity.X -= acceleration;
                    }
                    if (Projectile.velocity.Y < yDist) {
                        Projectile.velocity.Y = Projectile.velocity.Y + acceleration;
                        if (Projectile.velocity.Y < 0f && yDist > 0f)
                            Projectile.velocity.Y += acceleration;
                    } else if (Projectile.velocity.Y > yDist) {
                        Projectile.velocity.Y = Projectile.velocity.Y - acceleration;
                        if (Projectile.velocity.Y > 0f && yDist < 0f)
                            Projectile.velocity.Y -= acceleration;
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition
                , CWRUtils.GetRec(value, Projectile.frame, 7), Color.White
                , Projectile.rotation, CWRUtils.GetOrig(value, 7)
                , Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.FlipVertically : SpriteEffects.None);
            return false;
        }

        public override Color? GetAlpha(Color lightColor) {
            return new Color(255 - Projectile.alpha, 255 - Projectile.alpha, 255 - Projectile.alpha, 255 - Projectile.alpha);
        }

        public override void OnKill(int timeLeft) {
            Projectile.position = Projectile.Center;
            Projectile.width = Projectile.height = 238;
            Projectile.Center = Projectile.position;
            Projectile.maxPenetrate = -1;
            Projectile.penetrate = -1;
            Projectile.Damage();
            _ = SoundEngine.PlaySound(in SoundID.Item14, Projectile.position);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                new Vector2(0, 13), ModContent.ProjectileType<GhastlyBlasts>(), Projectile.damage, 2, Projectile.owner);
            for (int i = 0; i < 4; i++) {
                int dust = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 100, default, 1.5f);
                Main.dust[dust].position = Projectile.Center + (Vector2.UnitY.RotatedByRandom(3.1415927410125732) * (float)Main.rand.NextDouble() * Projectile.width / 2f);
            }

            for (int j = 0; j < 30; j++) {
                int dustIndex = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 200, default, 3.7f);
                Dust newDust = Main.dust[dustIndex];
                newDust.position = Projectile.Center + (Vector2.UnitY.RotatedByRandom(3.1415927410125732) * (float)Main.rand.NextDouble() * Projectile.width / 2f);
                newDust.noGravity = true;
                newDust.velocity *= 3f;
                _ = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 100, default, 1.5f);
                newDust.position = Projectile.Center + (Vector2.UnitY.RotatedByRandom(3.1415927410125732) * (float)Main.rand.NextDouble() * Projectile.width / 2f);
                newDust.velocity *= 2f;
                newDust.noGravity = true;
                newDust.fadeIn = 1f;
                newDust.color = Color.Crimson * 0.5f;
            }

            for (int k = 0; k < 10; k++) {
                int num3 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 0, default, 2.7f);
                Dust obj2 = Main.dust[num3];
                obj2.position = Projectile.Center + (Vector2.UnitX.RotatedByRandom(3.1415927410125732).RotatedBy(Projectile.velocity.ToRotation()) * Projectile.width / 2f);
                obj2.noGravity = true;
                obj2.velocity *= 3f;
            }

            for (int l = 0; l < 10; l++) {
                int num4 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 0, default, 1.5f);
                Dust obj3 = Main.dust[num4];
                obj3.position = Projectile.Center + (Vector2.UnitX.RotatedByRandom(3.1415927410125732).RotatedBy(Projectile.velocity.ToRotation()) * Projectile.width / 2f);
                obj3.noGravity = true;
                obj3.velocity *= 3f;
            }

            if (Main.myPlayer != Projectile.owner) {
                return;
            }

            for (int m = 0; m < Main.maxProjectiles; m++) {
                if (Main.projectile[m].active && Main.projectile[m].type == ModContent.ProjectileType<GhastlySubBlast>() && Main.projectile[m].ai[1] == Projectile.whoAmI) {
                    Main.projectile[m].Kill();
                }
            }
        }
    }
}
