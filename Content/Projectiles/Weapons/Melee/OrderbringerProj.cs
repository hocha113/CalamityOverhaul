using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Graphics.CameraModifiers;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class OrderbringerProj : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Orderbringer";
        public NPC Owner => Main.npc[(int)Projectile.ai[2]];
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (!Owner.Alives()) {
                Projectile.Kill();
                return;
            }
            if (Projectile.ai[1] != 1)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            if (Projectile.ai[0] == 0) {
                Projectile.velocity = new Vector2(0, -12);
            }
            if (Projectile.ai[1] == 0) {
                Projectile.scale += 0.01f;
                Projectile.velocity *= 0.97f;
                Projectile.position += Owner.velocity * 0.85f;
                if (Projectile.velocity.LengthSquared() < 9) {
                    Projectile.ai[1] = 1;
                    Projectile.ai[0] = 1;
                    Projectile.netUpdate = true;
                }
            }
            if (Projectile.ai[1] == 1) {
                if (Projectile.scale < 3) {
                    Projectile.scale += 0.02f;
                    if (!CWRUtils.isServer) {
                        for (int i = 0; i < 6; i++) {
                            Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.Next(133, 140) * Projectile.scale;
                            Vector2 particleSpeed = pos.To(Projectile.Center).UnitVector() * 17;
                            CWRParticle energyLeak = new LightParticle(pos, particleSpeed
                                , Main.rand.NextFloat(0.3f, 0.5f), Color.Purple, 16, 1, 1.5f, hueShift: 0.0f);
                            CWRParticleHandler.AddParticle(energyLeak);
                        }
                    }
                }
                else {
                    if (!CWRUtils.isServer) {
                        for (int i = 0; i < 6; i++) {
                            Vector2 randdom = Main.rand.NextVector2Unit();
                            Vector2 pos = Projectile.Center + randdom * Main.rand.Next(3, 14) * Projectile.scale;
                            Vector2 particleSpeed = randdom * 17;
                            CWRParticle energyLeak = new LightParticle(pos, particleSpeed
                                , Main.rand.NextFloat(0.1f, 0.6f), Color.Purple, 16, 1, 1.5f, hueShift: 0.0f);
                            CWRParticleHandler.AddParticle(energyLeak);
                        }
                    }
                }

                Projectile.velocity = Vector2.Zero;
                Projectile.damage += 35;
                Projectile.rotation += 0.2f;
                Projectile.position += Owner.velocity * 0.75f;
                if (Projectile.scale > 2) {
                    Projectile.ai[1] = 2;
                }
            }
            if (Projectile.ai[1] == 2) {
                NPC npc = Projectile.Center.FindClosestNPC(6000);
                if (npc != null) {
                    Projectile.ChasingBehavior(npc.Center, 36);
                    if (Projectile.Center.To(npc.Center).LengthSquared() < 16 * 16) {
                        Projectile.Damage();
                        Projectile.Kill();
                    }
                }
                else {
                    Projectile.Kill();
                }
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            }
            Projectile.ai[0]++;
        }

        public override bool? CanDamage() {
            if (Projectile.ai[1] != 2)
                return false;
            return base.CanDamage();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0;
            Vector2 vr = Projectile.rotation.ToRotationVector2();
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center + vr * -155
                ,  Projectile.Center + vr * 155, 155, ref point);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitNPC(target, hit, damageDone);
        }

        public override void OnKill(int timeLeft) {
            float angleStep = 0.05f;
            float radiusStep = 10f;
            float centerX = Projectile.Center.X;
            float centerY = Projectile.Center.Y;

            for (float angle = 0; angle < MathHelper.TwoPi * 5; angle += angleStep) {
                float radius = radiusStep * angle;
                float x = centerX + radius * (float)Math.Cos(angle);
                float y = centerY + radius * (float)Math.Sin(angle);

                // 在(x, y)处生成粒子或执行其他操作
                CWRParticle energyLeak = new LightParticle(new Vector2(x, y), Vector2.Zero
                        , 1.5f, CWRUtils.MultiStepColorLerp(angleStep % 1, Color.MediumPurple, Color.White), 120, 1, 1.5f, hueShift: 0.0f);
                CWRParticleHandler.AddParticle(energyLeak);
            }

            Projectile.Explode(300);
            PunchCameraModifier modifier = new PunchCameraModifier(Projectile.Center, (Main.rand.NextFloat() * ((float)Math.PI * 2f)).ToRotationVector2(), 30f, 6f, 20, 1000f, FullName);
            Main.instance.CameraModifiers.Add(modifier);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteEffects spriteEffects = SpriteEffects.None;
            if (Projectile.spriteDirection == -1)
                spriteEffects = SpriteEffects.FlipHorizontally;

            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Rectangle sourceRectangle = new Rectangle(0, 0, texture.Width + 12, texture.Height + 12);
            Vector2 origin = sourceRectangle.Size() / 2f;

            if (Projectile.ai[1] != 2) {
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    float rot = MathHelper.ToRadians(22.5f) * Math.Sign(Projectile.velocity.X);
                    Vector2 drawPos = Projectile.oldPos[i] - Main.screenPosition + origin + new Vector2(0f, Projectile.gfxOffY);
                    Color color = Projectile.GetAlpha(lightColor) * ((Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length);
                    Main.EntitySpriteDraw(texture, drawPos, new Rectangle?(), color, Projectile.rotation - i * rot, origin, Projectile.scale, spriteEffects, 0);
                }
            }
            else {
                Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White
                , Projectile.rotation, texture.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    Main.EntitySpriteDraw(texture, Projectile.oldPos[i] - Main.screenPosition + Projectile.Size / 2, null, Color.White * (1 - i * 0.1f)
                    , Projectile.rotation, texture.Size() / 2, Projectile.scale - i * 0.1f, SpriteEffects.None, 0);
                }
            }

            return false;
        }
    }
}
