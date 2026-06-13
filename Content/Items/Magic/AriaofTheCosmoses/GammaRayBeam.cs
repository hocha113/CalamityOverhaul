using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// 伽马射线
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
                beamWidth = MathHelper.Lerp(4f, maxBeamWidth, VaultUtils.EaseOutCubic(expandProgress));
                beamLength = MathHelper.Lerp(0f, maxBeamLength, VaultUtils.EaseOutQuad(expandProgress));
                coreIntensity = MathHelper.Lerp(0.5f, 1.5f, expandProgress);
            }
            else if (lifeRatio > 0.9f) {
                //收缩消失阶段
                float collapseProgress = (lifeRatio - 0.85f) / 0.15f;
                beamWidth = MathHelper.Lerp(maxBeamWidth, 4f, VaultUtils.EaseInQuad(collapseProgress));
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

            //伽马射线辐射光 紫蓝
            Lighting.AddLight(Projectile.Center,
                0.6f * coreIntensity,
                0.35f * coreIntensity,
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

            //电离闪烁火花 沿光束散射
            if (Main.rand.NextBool(4)) {
                float along = Main.rand.NextFloat(0.1f, 0.9f);
                Vector2 beamDir = Projectile.rotation.ToRotationVector2();
                Vector2 sparkPos = Projectile.Center + beamDir * beamLength * along
                    + beamDir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-beamWidth * 0.3f, beamWidth * 0.3f);
                Vector2 sparkVel = beamDir.RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f)) * Main.rand.NextFloat(1f, 3f);

                PRTLoader.NewParticle<PRT_Spark>(sparkPos, sparkVel, Color.Lerp(new Color(180, 140, 255), Color.White, Main.rand.NextFloat(0.3f, 0.7f)), Main.rand.NextFloat(0.6f, 1.1f)).Configure(false, Main.rand.Next(8, 15), Owner);
            }

            //高能射线流线 紫蓝
            if (Main.rand.NextBool(5)) {
                Vector2 lineStart = Projectile.Center + Main.rand.NextVector2Circular(beamWidth * 0.2f, beamWidth * 0.2f);
                Vector2 lineVel = Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(5f, 10f);

                PRTLoader.NewParticle<PRT_Line>(lineStart, lineVel, Color.Lerp(new Color(140, 100, 255), new Color(80, 180, 255), Main.rand.NextFloat()), Main.rand.NextFloat(0.4f, 0.9f)).Configure(false, Main.rand.Next(10, 18));
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
                //电离散射 紫蓝短线段
                for (int i = 0; i < 16; i++) {
                    float angle = MathHelper.TwoPi * i / 16f + Main.rand.NextFloat(-0.15f, 0.15f);
                    Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 12f);

                    PRTLoader.NewParticle<PRT_GammaIonize>(target.Center + Main.rand.NextVector2Circular(8f, 8f), velocity, Color.Lerp(new Color(160, 120, 255), Color.White, Main.rand.NextFloat(0.2f, 0.6f)), Main.rand.NextFloat(0.4f, 1.0f)).Configure(Main.rand.Next(12, 22), Main.rand.NextFloat(MathHelper.TwoPi));
                }

                //伽马冲击残影 Flashimpact
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi * i / 6f;
                    Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f);

                    PRTLoader.NewParticle<PRT_GammaImpact>(target.Center, velocity, Color.Lerp(new Color(140, 100, 255), new Color(80, 180, 255), Main.rand.NextFloat()), Main.rand.NextFloat(0.3f, 0.8f)).Configure(Main.rand.Next(15, 28), Main.rand.NextFloat(-0.2f, 0.2f), false, 0.3f);
                }

                //辐射光线 命中点向外高速光束
                float rand = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < 10; i++) {
                    float angle = MathHelper.TwoPi * i / 10f + rand;
                    Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(20f, 35f);

                    PRTLoader.NewParticle<PRT_Light>(
                        target.Center,
                        velocity,
                        Color.Lerp(new Color(160, 130, 255), new Color(200, 200, 255), Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.6f, 1.2f)
                    ).Configure(Main.rand.Next(18, 32), opacity: 1.5f, squishStrenght: 2f, hueShift: 0.015f);
                }
            }

            //穿透伤害递减
            Projectile.damage = (int)(Projectile.damage * 0.8f);
        }

        public override void OnKill(int timeLeft) {
            //伽马射线消散效果
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item62 with {
                    Volume = 0.5f,
                    Pitch = 0.5f
                }, Projectile.Center);

                //辐射残留电离线段 放射散开
                for (int i = 0; i < 18; i++) {
                    float angle = MathHelper.TwoPi * i / 18f;
                    Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 11f);

                    PRTLoader.NewParticle<PRT_GammaIonize>(Projectile.Center, velocity, Color.Lerp(new Color(140, 100, 255), new Color(80, 160, 255), Main.rand.NextFloat()), Main.rand.NextFloat(0.5f, 0.9f)).Configure(Main.rand.Next(15, 30), Main.rand.NextFloat(MathHelper.TwoPi));
                }

                //伽马冲击残影
                for (int i = 0; i < 10; i++) {
                    float angle = MathHelper.TwoPi * i / 10f;
                    Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 10f);

                    var burst = PRTLoader.NewParticle<PRT_GammaImpact>(Projectile.Center, velocity, Color.Lerp(new Color(160, 130, 255), Color.White, Main.rand.NextFloat(0.3f, 0.7f)), Main.rand.NextFloat(0.4f, 0.7f));
                    burst.Configure(Main.rand.Next(20, 35), Main.rand.NextFloat(-0.3f, 0.3f), false, 0.25f);
                    burst.inOwner = Owner.whoAmI;
                }
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            //伽马射线动态紫蓝色变化
            float colorShift = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.5f + 0.5f;
            return Color.Lerp(new Color(140, 100, 255), new Color(220, 200, 255), colorShift * coreIntensity);
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
                new Color(180, 140, 255) * (1f - Projectile.alpha / 255f),
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
            //绘制伽马核心发光层 紫蓝白
            Texture2D glowTexture = CWRAsset.StarTexture.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            for (int i = 0; i < 4; i++) {
                float scale = (beamWidth / glowTexture.Width) * (1.3f - i * 0.25f) * coreIntensity;
                float a = (1f - i * 0.25f) * pulseIntensity;

                //核心白紫 外层蓝紫
                Color glowColor = Color.Lerp(
                    new Color(220, 190, 255),
                    new Color(100, 60, 220), i / 3f) * a;

                sb.Draw(
                    glowTexture,
                    drawPos,
                    null,
                    glowColor,
                    Projectile.rotation,
                    new Vector2(0, glowTexture.Height / 2f),
                    new Vector2(beamLength / glowTexture.Width * 0.85f, scale),
                    SpriteEffects.None,
                    0f
                );
            }

            //切伦科夫辐射光晕 薄蓝光
            float cherenkovAlpha = pulseIntensity * 0.3f;
            float cherenkovScale = (beamWidth / glowTexture.Width) * 1.8f * coreIntensity;
            sb.Draw(
                glowTexture,
                drawPos,
                null,
                new Color(80, 160, 255) * cherenkovAlpha,
                Projectile.rotation,
                new Vector2(0, glowTexture.Height / 2f),
                new Vector2(beamLength / glowTexture.Width * 0.9f, cherenkovScale),
                SpriteEffects.None,
                0f
            );
        }
    }
}
