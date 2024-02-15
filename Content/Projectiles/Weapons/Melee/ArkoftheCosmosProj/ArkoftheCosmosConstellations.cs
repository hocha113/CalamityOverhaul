using CalamityMod;
using CalamityMod.Particles;
using CalamityOverhaul.Content.Items.Melee;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.ArkoftheCosmosProj
{
    /// <summary>
    /// 星链
    /// </summary>
    internal class ArkoftheCosmosConstellations : ModProjectile
    {
        private const float ConstellationSwapTime = 15f;

        public List<Particle> Particles;

        public new string LocalizationCategory => "Projectiles.Melee";

        public override string Texture => "CalamityMod/Projectiles/InvisibleProj";

        public Player Owner => Main.player[Projectile.owner];

        public float Timer => Projectile.ai[0] - Projectile.timeLeft;

        private Vector2 AnchorStart => Owner.Center;

        private Vector2 AnchorEnd => Owner.Calamity().mouseWorld;

        public Vector2 SizeVector => (AnchorEnd - AnchorStart).SafeNormalize(Vector2.Zero) * MathHelper.Clamp((AnchorEnd - AnchorStart).Length(), 0f, GetNewThrowReach());

        private float GetNewThrowReach() {
            float lengs = ArkoftheCosmos.MaxThrowReach;
            if (Projectile.IsOwnedByLocalPlayer()) {
                lengs = Owner.Center.To(Main.MouseWorld).Length();
            }
            return lengs;
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 1;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + SizeVector, 30f, ref collisionPoint);
        }

        public void BootlegSpawnParticle(Particle particle) {
            if (!Main.dedServ) {
                Particles.Add(particle);

                Type particleHandlerType = typeof(GeneralParticleHandler);//oh，这里似乎只能用反射解决问题了  
                FieldInfo particleTypesField = particleHandlerType.GetField("particleTypes", BindingFlags.NonPublic | BindingFlags.Static);
                if (particleTypesField != null) {
                    Dictionary<Type, int> particleTypesValue = (Dictionary<Type, int>)particleTypesField.GetValue(null);
                    particle.Type = particleTypesValue[particle.GetType()];
                }
                else {
                    CWRUtils.Text(CWRUtils.Translation(
                        "未能获取到 GeneralParticleHandler 类的成员 particleTypes ,请检查对象是否存在\n" +
                        "如果您是在游玩中看到此条信息，可以向coslowkimberli185@gmail.com发送反馈信息，我会尽快修复问题",
                        "A member of the GeneralParticleHandler class, particleTypes, could not be obtained. Please check whether the object exists\n" +
                        "If you saw this while playing, you can send a feedback to coslowkimberli185@gmail.com and I'll fix it as soon as possible"
                        ));
                    Projectile.Kill();
                    return;
                }
            }
        }

        public override void AI() {
            if (Particles == null) {
                Particles = new List<Particle>();
            }
            Projectile.Center = Owner.Center;
            if (!Owner.channel && Projectile.timeLeft > 20) {
                Projectile.timeLeft = 20;
            }

            if (!Owner.active) {
                Projectile.Kill();
                return;
            }

            if (Projectile.ai[1] == 1) {
                Projectile projectile = CWRUtils.GetProjectileInstance((int)Projectile.ai[2]);
                if (projectile == null) {
                    Projectile.Kill();
                    return;
                }
            }

            if (Timer % 15f == 0f && Projectile.timeLeft >= 20) {
                Particles.Clear();
                float num = Main.rand.NextFloat();
                Color color = Main.hslToRgb(num, 1f, 0.8f);
                Vector2 vector = AnchorStart;
                Particle particle2 = new GenericSparkle(vector, Vector2.Zero, Color.White, Color.Plum, Main.rand.NextFloat(1f, 1.5f), 20, 0f, 3f);
                BootlegSpawnParticle(particle2);
                Particle particle3;
                for (float num2 = 0f + Main.rand.NextFloat(0.2f, 0.5f); num2 < 1f; num2 += Main.rand.NextFloat(0.2f, 0.5f)) {
                    num = (num + 0.16f) % 1f;
                    color = Main.hslToRgb(num, 1f, 0.8f);
                    Vector2 vector2 = Main.rand.NextFloat(-50f, 50f) * SizeVector.RotatedBy(1.5707963705062866).SafeNormalize(Vector2.Zero);
                    particle2 = new GenericSparkle(AnchorStart + SizeVector * num2 + vector2, Vector2.Zero, Color.White, color, Main.rand.NextFloat(1f, 1.5f), 20, 0f, 3f);
                    BootlegSpawnParticle(particle2);
                    particle3 = new BloomLineVFX(vector, AnchorStart + SizeVector * num2 + vector2 - vector, 0.8f, color * 0.75f, 20, capped: true, telegraph: true);
                    BootlegSpawnParticle(particle3);
                    if (Main.rand.NextBool(3)) {
                        num = (num + 0.16f) % 1f;
                        color = Main.hslToRgb(num, 1f, 0.8f);
                        vector2 = Main.rand.NextFloat(-50f, 50f) * SizeVector.RotatedBy(1.5707963705062866).SafeNormalize(Vector2.Zero);
                        particle2 = new GenericSparkle(AnchorStart + SizeVector * num2 + vector2, Vector2.Zero, Color.White, color, Main.rand.NextFloat(1f, 1.5f), 20, 0f, 3f);
                        BootlegSpawnParticle(particle2);
                        particle3 = new BloomLineVFX(vector, AnchorStart + SizeVector * num2 + vector2 - vector, 0.8f, color, 20, capped: true, telegraph: true);
                        BootlegSpawnParticle(particle3);
                    }

                    vector = AnchorStart + SizeVector * num2 + vector2;
                }

                num = (num + 0.16f) % 1f;
                color = Main.hslToRgb(num, 1f, 0.8f);
                particle2 = new GenericSparkle(AnchorStart + SizeVector, Vector2.Zero, Color.White, color, Main.rand.NextFloat(1f, 1.5f), 20, 0f, 3f);
                BootlegSpawnParticle(particle2);
                particle3 = new BloomLineVFX(vector, AnchorStart + SizeVector - vector, 0.8f, color * 0.75f, 20, capped: true);
                BootlegSpawnParticle(particle3);
            }

            Vector2 vector3 = Vector2.Zero;
            if (Timer > Projectile.oldPos.Length) {
                vector3 = Projectile.position - Projectile.oldPos[0];
            }

            foreach (Particle particle4 in Particles) {
                if (particle4 != null) {
                    particle4.Position += particle4.Velocity + vector3;
                    particle4.Time++;
                    particle4.Update();
                    particle4.Color = Main.hslToRgb(Main.rgbToHsl(particle4.Color).X + 0.02f, Main.rgbToHsl(particle4.Color).Y, Main.rgbToHsl(particle4.Color).Z);
                }
            }

            Particles.RemoveAll((particle) => particle.Time >= particle.Lifetime && particle.SetLifetime);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Particles != null) {
                Main.spriteBatch.EnterShaderRegion(BlendState.Additive);
                foreach (Particle particle in Particles) {
                    particle.CustomDraw(Main.spriteBatch);
                }

                Main.spriteBatch.ExitShaderRegion();
            }

            return false;
        }
    }
}
