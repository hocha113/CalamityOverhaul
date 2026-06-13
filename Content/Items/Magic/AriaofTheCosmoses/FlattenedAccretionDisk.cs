using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// 压扁3D吸积盘
    internal class FlattenedAccretionDisk : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        //吸积盘参数
        public ref float RotationSpeed => ref Projectile.ai[0];
        public ref float FlattenAngle => ref Projectile.ai[1]; //压扁角度 3D效果
        public ref float ChargeProgress => ref Projectile.ai[2]; //蓄力进度

        private float time;
        private float brightness = 1f;
        private float distortionStrength = 0.15f;
        private float pulseIntensity = 0f;

        //颜色配置
        private Color innerColor = new Color(255, 200, 100); //内圈
        private Color midColor = new Color(255, 120, 50);    //中圈
        private Color outerColor = new Color(100, 50, 150);  //外圈

        private int gammaRayTimer = 0;
        private const int GammaRayInterval = 30; //伽马射线间隔

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 800;
            Projectile.height = 800;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            //淡入效果
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 8;
            }

            time += 0.016f;

            //默认参数设置
            if (RotationSpeed == 0) {
                RotationSpeed = 1.5f;
            }
            if (FlattenAngle == 0) {
                FlattenAngle = 0.65f; //默认压扁角度
            }

            //脉动效果
            brightness = 1f;

            //根据蓄力进度调整脉动强度
            pulseIntensity = ChargeProgress * 0.3f;

            //生成环绕粒子
            if (Projectile.timeLeft % 2 == 0 && !Main.dedServ) {
                SpawnDiskParticles();
            }

            //蓄力完成后定期发射伽马射线
            if (ChargeProgress >= 0.8f) {
                gammaRayTimer++;
                if (gammaRayTimer >= 8) {
                    ShootGammaRay();
                    gammaRayTimer = 0;
                }
            }

            //淡出效果
            if (Projectile.timeLeft < 60) {
                Projectile.alpha += 5;
                brightness *= Projectile.timeLeft / 60f;
            }

            //发光
            Lighting.AddLight(Projectile.Center,
                innerColor.ToVector3() * brightness * 1.2f * (1f - Projectile.alpha / 255f));
        }

        private void SpawnDiskParticles() {
            //在吸积盘边缘生成粒子
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(0.3f, 0.9f) * Projectile.width * 0.5f * Projectile.scale;

            //考虑压扁效果的Y轴缩放
            Vector2 offset = new Vector2(
                (float)Math.Cos(angle) * distance,
                (float)Math.Sin(angle) * distance * FlattenAngle
            );

            Vector2 particlePos = Projectile.Center + offset;
            Vector2 particleVel = Vector2.Normalize(offset.RotatedBy(MathHelper.PiOver2)) * Main.rand.NextFloat(0.5f, 2f);

            int dustType = Main.rand.Next(new[] { 59, 60, 62, 135 }); //蓝色系粒子
            Dust dust = Dust.NewDustPerfect(particlePos, dustType, particleVel, 100,
                Color.Lerp(innerColor, outerColor, Main.rand.NextFloat()), Main.rand.NextFloat(1.2f, 2f));
            dust.noGravity = true;
            dust.fadeIn = 1.2f;
        }

        private void ShootGammaRay() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            //播放射线音效
            SoundEngine.PlaySound(SoundID.Item72 with {
                Volume = 0.6f,
                Pitch = -0.2f
            }, Projectile.Center);

            //寻找最近的敌人
            NPC target = null;
            float minDistance = 900f;

            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }

                float distance = Vector2.Distance(Projectile.Center, npc.Center);
                if (distance < minDistance) {
                    minDistance = distance;
                    target = npc;
                }
            }

            //发射伽马射线
            Vector2 shootDirection = Projectile.rotation.ToRotationVector2().RotatedByRandom(0.2f);

            //射线伤害随蓄力进度提升
            int damage = (int)(Projectile.damage * (0.4f + ChargeProgress * 0.3f));

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                shootDirection * 2f,
                ModContent.ProjectileType<GammaRayBeam>(),
                damage,
                2f,
                Projectile.owner
            );

            //发射伽马射线电离闪光
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    Vector2 sparkVel = shootDirection.RotatedByRandom(0.5f) * Main.rand.NextFloat(5f, 12f);
                    Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, sparkVel, 100,
                        new Color(160, 120, 255), Main.rand.NextFloat(1.2f, 2f));
                    spark.noGravity = true;
                }

                //辐射环
                for (int i = 0; i < 10; i++) {
                    float angle = MathHelper.TwoPi * i / 10f;
                    Vector2 offset = angle.ToRotationVector2() * 30f;

                    Dust shockwave = Dust.NewDustPerfect(Projectile.Center + offset, DustID.PurpleTorch,
                        offset.SafeNormalize(Vector2.Zero) * 2f, 100,
                        new Color(100, 60, 200), Main.rand.NextFloat(1.2f, 1.8f));
                    shockwave.noGravity = true;
                }
            }
        }

        public override void OnKill(int timeLeft) {
            //消失特效
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item92 with {
                Volume = 0.6f,
                Pitch = 0.2f
            }, Projectile.Center);

            //爆发粒子
            for (int i = 0; i < 40; i++) {
                float angle = MathHelper.TwoPi * i / 40f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 10f);
                velocity.Y *= FlattenAngle; //保持压扁效果

                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.BlueTorch, velocity, 100,
                    Color.Lerp(innerColor, outerColor, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1.5f, 2.5f));
                dust.noGravity = true;
            }
        }

        public void DrawPrimitives() {
            if (VaultUtils.isServer) {
                return;
            }

            DrawFlattenedAccretionDisk();
        }

        [VaultLoaden(CWRConstant.Masking)]
        private static Texture2D TransverseTwill = null!;

        private void DrawFlattenedAccretionDisk() {
            SpriteBatch spriteBatch = Main.spriteBatch;
            float alpha = 1f - Projectile.alpha / 255f;
            if (alpha <= 0f) return;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float actualWidth = Projectile.width * Projectile.scale;
            float actualHeight = Projectile.height * Projectile.scale * FlattenAngle;

            Matrix finalMatrix = Matrix.Identity
                * Main.GameViewMatrix.TransformationMatrix
                * Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);

            Vector2 screenCenter = Projectile.Center - Main.screenPosition;
            Vector2 texHalf = TransverseTwill.Size() * 0.5f;
            Vector2 diskScale = new Vector2(actualWidth / TransverseTwill.Width, actualHeight / TransverseTwill.Height);

            //绘制吸积盘主体
            {
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                Effect shader = EffectLoader.FlattenedDisk.Value;

                shader.Parameters["transformMatrix"]?.SetValue(finalMatrix);
                shader.Parameters["uTime"]?.SetValue(time);
                shader.Parameters["rotationSpeed"]?.SetValue(RotationSpeed);
                shader.Parameters["flattenRatio"]?.SetValue(FlattenAngle);
                shader.Parameters["brightness"]?.SetValue(brightness * 1.5f);
                shader.Parameters["distortionStrength"]?.SetValue(distortionStrength * 1.2f);
                shader.Parameters["pulseIntensity"]?.SetValue(pulseIntensity);
                shader.Parameters["dopplerStrength"]?.SetValue(0.6f);
                shader.Parameters["noiseTexture"]?.SetValue(TransverseTwill);
                shader.Parameters["centerPos"]?.SetValue(screenCenter);
                shader.Parameters["innerColor"]?.SetValue(innerColor.ToVector4());
                shader.Parameters["midColor"]?.SetValue(midColor.ToVector4());
                shader.Parameters["outerColor"]?.SetValue(outerColor.ToVector4());

                Main.graphics.GraphicsDevice.Textures[1] = TransverseTwill;
                Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

                shader.CurrentTechnique.Passes["FlattenedDiskPass"].Apply();

                //外层辉光
                for (int i = 0; i < 2; i++) {
                    float s = 1.6f + i * 0.5f;
                    float a = alpha * (0.2f - i * 0.06f);
                    spriteBatch.Draw(TransverseTwill, drawPos, null,
                        Color.White * a,
                        Projectile.rotation + i * 0.12f + MathHelper.PiOver2,
                        texHalf, diskScale * s, SpriteEffects.None, 0);
                }

                //核心盘面层
                for (int i = 0; i < 3; i++) {
                    float s = 0.85f + i * 0.12f;
                    spriteBatch.Draw(TransverseTwill, drawPos, null,
                        Color.White * alpha * 0.85f,
                        Projectile.rotation + i * 0.03f + MathHelper.PiOver2,
                        texHalf, diskScale * s, SpriteEffects.None, 0);
                }

                spriteBatch.End();
            }

            //绘制中央天体（黑洞/能量核心）
            {
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                Effect shader = EffectLoader.AccretionDisk.Value;
                float orbSize = Projectile.width * Projectile.scale * 0.25f;

                shader.Parameters["transformMatrix"]?.SetValue(finalMatrix);
                shader.Parameters["uTime"]?.SetValue(time);
                shader.Parameters["rotationSpeed"]?.SetValue(RotationSpeed * 1.2f);
                shader.Parameters["innerRadius"]?.SetValue(0.12f);
                shader.Parameters["outerRadius"]?.SetValue(0.85f);
                shader.Parameters["brightness"]?.SetValue(brightness * 1.5f);
                shader.Parameters["distortionStrength"]?.SetValue(distortionStrength * 0.3f);
                shader.Parameters["noiseTexture"]?.SetValue(TransverseTwill);
                shader.Parameters["centerPos"]?.SetValue(screenCenter);
                shader.Parameters["innerColor"]?.SetValue(innerColor.ToVector4());
                shader.Parameters["midColor"]?.SetValue(midColor.ToVector4());
                shader.Parameters["outerColor"]?.SetValue(outerColor.ToVector4());

                Main.graphics.GraphicsDevice.Textures[1] = TransverseTwill;
                Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

                shader.CurrentTechnique.Passes["AccretionDiskPass"].Apply();

                Vector2 orbDrawPos = drawPos + Projectile.rotation.ToRotationVector2() * 8f * Projectile.scale;
                Vector2 orbScale = new Vector2(orbSize / TransverseTwill.Width, orbSize / TransverseTwill.Height);

                for (int i = 0; i < 3; i++) {
                    float s = 0.75f + i * 0.2f;
                    spriteBatch.Draw(TransverseTwill, orbDrawPos, null,
                        Color.White * alpha * 0.85f,
                        Projectile.rotation + i * 0.1f,
                        texHalf, orbScale * s, SpriteEffects.None, 0);
                }

                spriteBatch.End();
            }
        }
    }
}
