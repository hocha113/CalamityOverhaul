using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using InnoVault.PRT;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// <summary>
    /// 伽马射线
    /// </summary>
    internal class GammaRayBeam : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int MaxTrailLength = 30;
        private float beamWidth = 22f;
        private float maxBeamWidth = 65f;
        private float beamLength = 0f;
        private float maxBeamLength = 2200f;

        //视觉效果参数
        private float pulseIntensity = 1f;
        private float coreIntensity = 1f;
        private float distortionStrength = 0.15f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = MaxTrailLength;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 2;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * beamLength, beamWidth, ref p);
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.position -= Projectile.velocity;

            //光束展开和收缩动画
            float lifeRatio = 1f - Projectile.timeLeft / 300f;

            if (lifeRatio < 0.1f) {
                //快速展开阶段
                float expandProgress = lifeRatio / 0.15f;
                beamWidth = MathHelper.Lerp(4f, maxBeamWidth, CWRUtils.EaseOutCubic(expandProgress));
                beamLength = MathHelper.Lerp(0f, maxBeamLength, CWRUtils.EaseOutQuad(expandProgress));
                coreIntensity = MathHelper.Lerp(0.5f, 1.5f, expandProgress);
            }
            else if (lifeRatio > 0.9f) {
                //收缩消失阶段
                float collapseProgress = (lifeRatio - 0.85f) / 0.15f;
                beamWidth = MathHelper.Lerp(maxBeamWidth, 4f, CWRUtils.EaseInQuad(collapseProgress));
                coreIntensity = MathHelper.Lerp(1.5f, 0f, collapseProgress);
            }
            else {
                //稳定阶段
                beamWidth = maxBeamWidth;
                beamLength = maxBeamLength;

                //脉动效果
                float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.1f + 0.9f;
                pulseIntensity = pulse;
                coreIntensity = 1.2f + pulse * 0.3f;
            }

            //能量粒子特效
            SpawnEnergyParticles();

            //发光效果
            Lighting.AddLight(Projectile.Center,
                0.6f * coreIntensity,
                0.9f * coreIntensity,
                1.2f * coreIntensity);

            //音效
            if (Projectile.timeLeft % 30 == 0) {
                SoundEngine.PlaySound(SoundID.Item15 with {
                    Volume = 0.3f,
                    Pitch = 0.6f,
                    SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
                }, Projectile.Center);
            }

            Vector2 toMus = ToMouse;
            Projectile.Center = Owner.Center;
            if (Projectile.localAI[0] == 0) {
                Projectile.localAI[0] = Projectile.rotation - toMus.ToRotation();
            }
            Projectile.rotation = toMus.ToRotation() + Projectile.localAI[0];
        }

        private void SpawnEnergyParticles() {
            if (VaultUtils.isServer || Projectile.timeLeft % 2 != 0) {
                return;
            }

            //星光闪烁
            if (Main.rand.NextBool(6)) {
                Vector2 sparkPos = Projectile.Center + Main.rand.NextVector2Circular(beamWidth * 0.4f, beamWidth * 0.4f);
                Vector2 sparkVel = Main.rand.NextVector2Circular(1f, 1f);
                
                BasePRT spark = new PRT_Spark(
                    sparkPos,
                    sparkVel,
                    false,
                    Main.rand.Next(10, 18),
                    Main.rand.NextFloat(0.8f, 1.3f),
                    Color.White,
                    Owner
                );
                PRTLoader.AddParticle(spark);
            }

            //能量流动线条
            if (Main.rand.NextBool(5)) {
                Vector2 lineStart = Projectile.Center + Main.rand.NextVector2Circular(beamWidth * 0.3f, beamWidth * 0.3f);
                Vector2 lineVel = Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);
                
                BasePRT line = new PRT_Line(
                    lineStart,
                    lineVel,
                    false,
                    Main.rand.Next(12, 20),
                    Main.rand.NextFloat(0.5f, 1f),
                    Color.Lerp(Color.Cyan, new Color(150, 220, 255), Main.rand.NextFloat())
                );
                PRTLoader.AddParticle(line);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits != 0) {
                return;
            }

            //击中爆发效果
            SoundEngine.PlaySound(SoundID.Item94 with {
                Volume = 0.5f,
                Pitch = 0.4f
            }, Projectile.Center);

            if (!VaultUtils.isServer) {
                //爆发冲击粒子
                for (int i = 0; i < 12; i++) {
                    float angle = MathHelper.TwoPi * i / 12f;
                    Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 9f);

                    BasePRT impactBurst = new PRT_GammaImpact(
                        target.Center,
                        velocity,
                        Color.Lerp(Color.Cyan, Color.White, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.2f, 1.2f),
                        Main.rand.Next(20, 35),
                        Main.rand.NextFloat(-0.3f, 0.3f),
                        false,
                        0.25f
                    );
                    PRTLoader.AddParticle(impactBurst);
                }

                float rand = Main.rand.NextFloat(MathHelper.TwoPi);
                //光芒粒子
                for (int i = 0; i < 15; i++) {
                    float angle = MathHelper.TwoPi * i / 15f + rand;
                    Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(23f, 37f);

                    BasePRT light = new PRT_Light(
                        target.Center,
                        velocity,
                        Main.rand.NextFloat(0.8f, 1.5f),
                        Color.Lerp(Color.Cyan, Color.White, Main.rand.NextFloat()),
                        Main.rand.Next(25, 40),
                        1.5f,
                        2f,
                        hueShift: 0.02f
                    );
                    PRTLoader.AddParticle(light);
                }

                //冲击波环
                for (int i = 0; i < 20; i++) {
                    float angle = MathHelper.TwoPi * i / 20f;
                    Vector2 offset = angle.ToRotationVector2() * 30f;
                    Vector2 velocity = offset.SafeNormalize(Vector2.Zero) * 5f;

                    BasePRT shock = new PRT_Spark(
                        target.Center + offset,
                        velocity,
                        false,
                        Main.rand.Next(15, 25),
                        Main.rand.NextFloat(1f, 1.5f),
                        new Color(100, 200, 255),
                        Owner
                    );
                    PRTLoader.AddParticle(shock);
                }
            }

            //穿透伤害递减
            Projectile.damage = (int)(Projectile.damage * 0.8f);
        }

        public override void OnKill(int timeLeft) {
            //消失爆炸效果
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item62 with {
                    Volume = 0.5f,
                    Pitch = 0.3f
                }, Projectile.Center);

                //放射状冲击粒子
                for (int i = 0; i < 24; i++) {
                    float angle = MathHelper.TwoPi * i / 24f;
                    Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 13f);

                    PRT_GammaImpact burst = new PRT_GammaImpact(
                        Projectile.Center,
                        velocity,
                        Color.Lerp(Color.Cyan, Color.White, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.5f, 0.75f),
                        Main.rand.Next(30, 45),
                        Main.rand.NextFloat(-0.4f, 0.4f),
                        false,
                        0.3f
                    );
                    burst.inOwner = Owner.whoAmI;
                    PRTLoader.AddParticle(burst);
                }

                //内爆收缩粒子
                for (int i = 0; i < 15; i++) {
                    Vector2 spawnPos = Projectile.Center + Main.rand.NextVector2Circular(90f, 90f);
                    Vector2 velocity = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(10f, 18f);

                    BasePRT implosion = new PRT_Spark(
                        spawnPos,
                        velocity,
                        false,
                        Main.rand.Next(20, 30),
                        Main.rand.NextFloat(1f, 1.8f),
                        Color.White,
                        Owner
                    );
                    PRTLoader.AddParticle(implosion);
                }
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            //动态颜色变化
            float colorShift = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.5f + 0.5f;
            return Color.Lerp(Color.Cyan, Color.White, colorShift * coreIntensity);
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawGammaBeam();
            return false;
        }

        private void DrawGammaBeam() {
            if (VaultUtils.isServer) {
                return;
            }

            SpriteBatch sb = Main.spriteBatch;

            //准备渲染
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Effect shader = Common.EffectLoader.GammaRayBeam.Value;

            //设置着色器参数
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uOpacity"]?.SetValue(1f - Projectile.alpha / 255f);
            shader.Parameters["uIntensity"]?.SetValue(pulseIntensity);
            shader.Parameters["uBeamWidth"]?.SetValue(beamWidth);
            shader.Parameters["uBeamLength"]?.SetValue(beamLength);
            shader.Parameters["uPulseSpeed"]?.SetValue(5f);
            shader.Parameters["uDistortionStrength"]?.SetValue(distortionStrength);
            shader.Parameters["uCoreIntensity"]?.SetValue(coreIntensity);

            //设置纹理
            shader.Parameters["uImage1"]?.SetValue(CWRAsset.Extra_193.Value); //噪声纹理
            shader.Parameters["uImage2"]?.SetValue(CWRAsset.StarTexture.Value); //星光纹理
            shader.Parameters["uImage3"]?.SetValue(CWRAsset.Placeholder_White.Value); //光束纹理

            shader.CurrentTechnique.Passes["GammaRayPass"].Apply();

            //绘制主光束
            Texture2D beamTexture = CWRAsset.Placeholder_White.Value;
            Vector2 beamOrigin = new Vector2(0, beamTexture.Height / 2f);
            Vector2 beamScale = new Vector2(beamLength / beamTexture.Width, beamWidth / beamTexture.Height);

            sb.Draw(
                beamTexture,
                Projectile.Center - Main.screenPosition,
                null,
                new Color(255, 200, 100) * (1f - Projectile.alpha / 255f),
                Projectile.rotation,
                beamOrigin,
                beamScale,
                SpriteEffects.None,
                0f
            );

            //绘制核心高光层
            DrawCoreHighlight(sb);

            //恢复默认渲染状态
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawCoreHighlight(SpriteBatch sb) {
            //绘制额外的核心发光层
            Texture2D glowTexture = CWRAsset.StarTexture.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            for (int i = 0; i < 13; i++) {
                float scale = (beamWidth / glowTexture.Width) * (1.2f - i * 0.2f) * coreIntensity;
                float alpha = (1f - i * 0.3f) * pulseIntensity;

                Color glowColor = Color.Lerp(new Color(255, 200, 100), new Color(255, 120, 50), i / 3f) * alpha;

                sb.Draw(
                    glowTexture,
                    drawPos,
                    null,
                    glowColor,
                    Projectile.rotation,
                    new Vector2(0, glowTexture.Height / 2f),
                    new Vector2(beamLength / glowTexture.Width * 0.8f, scale),
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
}
