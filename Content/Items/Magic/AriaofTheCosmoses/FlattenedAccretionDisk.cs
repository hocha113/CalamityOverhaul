using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// <summary>
    /// 压扁的3D吸积盘 - 右键蓄力专用
    /// </summary>
    internal class FlattenedAccretionDisk : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        //吸积盘参数
        public ref float RotationSpeed => ref Projectile.ai[0];
        public ref float FlattenAngle => ref Projectile.ai[1]; //压扁角度，用于实现3D效果
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
        private const int GammaRayInterval = 30; //伽马射线发射间隔

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 500;
            Projectile.height = 500;
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

            //生成射线特效
            for (int i = 0; i < 12; i++) {
                Vector2 sparkVel = shootDirection.RotatedByRandom(0.4f) * Main.rand.NextFloat(4f, 10f);
                Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, sparkVel, 100,
                    Color.Cyan, Main.rand.NextFloat(1.2f, 2f));
                spark.noGravity = true;
            }

            //冲击波
            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16f;
                Vector2 offset = angle.ToRotationVector2() * 40f;

                Dust shockwave = Dust.NewDustPerfect(Projectile.Center + offset, DustID.BlueTorch,
                    offset.SafeNormalize(Vector2.Zero) * 3f, 100,
                    Color.DeepSkyBlue, Main.rand.NextFloat(1.5f, 2f));
                shockwave.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            //消失特效
            if (!VaultUtils.isServer) {
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
        }

        public void DrawPrimitives() {
            if (VaultUtils.isServer) {
                return;
            }

            DrawFlattenedAccretionDisk();
        }

        [VaultLoaden(CWRConstant.Masking)]
        private static Texture2D TransverseTwill;

        private void DrawFlattenedAccretionDisk() {
            SpriteBatch spriteBatch = Main.spriteBatch;

            //绘制主要的压扁吸积盘
            {
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                Effect shader = EffectLoader.FlattenedDisk.Value;

                float actualWidth = Projectile.width * Projectile.scale;
                float actualHeight = Projectile.height * Projectile.scale * FlattenAngle; //应用压扁效果

                Matrix world = Matrix.Identity;
                Matrix view = Main.GameViewMatrix.TransformationMatrix;
                Matrix projection = Matrix.CreateOrthographicOffCenter(
                    0, Main.screenWidth,
                    Main.screenHeight, 0,
                    -1, 1);

                Matrix finalMatrix = world * view * projection;

                shader.Parameters["transformMatrix"]?.SetValue(finalMatrix);
                shader.Parameters["uTime"]?.SetValue(time);
                shader.Parameters["rotationSpeed"]?.SetValue(RotationSpeed);
                shader.Parameters["flattenRatio"]?.SetValue(FlattenAngle);
                shader.Parameters["brightness"]?.SetValue(brightness);
                shader.Parameters["distortionStrength"]?.SetValue(distortionStrength);
                shader.Parameters["pulseIntensity"]?.SetValue(pulseIntensity);
                shader.Parameters["noiseTexture"]?.SetValue(VaultAsset.placeholder2.Value);

                Vector2 screenCenter = Projectile.Center - Main.screenPosition;
                shader.Parameters["centerPos"]?.SetValue(screenCenter);

                shader.Parameters["innerColor"]?.SetValue(innerColor.ToVector4());
                shader.Parameters["midColor"]?.SetValue(midColor.ToVector4());
                shader.Parameters["outerColor"]?.SetValue(outerColor.ToVector4());

                Main.graphics.GraphicsDevice.Textures[1] = TransverseTwill;
                Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

                shader.CurrentTechnique.Passes["FlattenedDiskPass"].Apply();

                Vector2 drawPosition = Projectile.Center - Main.screenPosition;

                for (int j = 0; j < 6; j++) {
                    //绘制多层增强立体感
                    for (int i = 0; i < 13; i++) {
                        float layerScale = 1f + j - i * 0.25f;
                        spriteBatch.Draw(
                            TransverseTwill,
                            drawPosition,
                            null,
                            Color.White * (1f - Projectile.alpha / 255f) * (1f - i * 0.3f),
                            Projectile.rotation + i * 0.1f + MathHelper.PiOver2,
                            TransverseTwill.Size() * 0.5f,
                            new Vector2(actualWidth / TransverseTwill.Width, actualHeight / TransverseTwill.Height) * layerScale,
                            SpriteEffects.None,
                            0
                        );
                    }
                }


                spriteBatch.End();
            }


            {
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                Effect shader = EffectLoader.FlattenedDisk.Value;

                float actualWidth = Projectile.width * Projectile.scale;
                float actualHeight = Projectile.height * Projectile.scale * FlattenAngle; //应用压扁效果

                Matrix world = Matrix.Identity;
                Matrix view = Main.GameViewMatrix.TransformationMatrix;
                Matrix projection = Matrix.CreateOrthographicOffCenter(
                    0, Main.screenWidth,
                    Main.screenHeight, 0,
                    -1, 1);

                Matrix finalMatrix = world * view * projection;

                shader.Parameters["transformMatrix"]?.SetValue(finalMatrix);
                shader.Parameters["uTime"]?.SetValue(time);
                shader.Parameters["rotationSpeed"]?.SetValue(RotationSpeed);
                shader.Parameters["flattenRatio"]?.SetValue(FlattenAngle);
                shader.Parameters["brightness"]?.SetValue(brightness);
                shader.Parameters["distortionStrength"]?.SetValue(distortionStrength);
                shader.Parameters["pulseIntensity"]?.SetValue(pulseIntensity);
                shader.Parameters["noiseTexture"]?.SetValue(TransverseTwill);

                Vector2 screenCenter = Projectile.Center - Main.screenPosition;
                shader.Parameters["centerPos"]?.SetValue(screenCenter);

                shader.Parameters["innerColor"]?.SetValue(innerColor.ToVector4());
                shader.Parameters["midColor"]?.SetValue(midColor.ToVector4());
                shader.Parameters["outerColor"]?.SetValue(outerColor.ToVector4());

                Main.graphics.GraphicsDevice.Textures[1] = TransverseTwill;
                Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

                shader.CurrentTechnique.Passes["FlattenedDiskPass"].Apply();

                Vector2 drawPosition = Projectile.Center - Main.screenPosition;

                for (int j = 0; j < 3; j++) {
                    //绘制多层增强立体感
                    for (int i = 0; i < 13; i++) {
                        float layerScale = 1f + j - i * 0.25f;
                        spriteBatch.Draw(
                            TransverseTwill,
                            drawPosition,
                            null,
                            Color.White * (1f - Projectile.alpha / 255f) * (1f - i * 0.3f),
                            Projectile.rotation + i * 0.1f + MathHelper.PiOver2,
                            TransverseTwill.Size() * 0.5f,
                            new Vector2(actualWidth / TransverseTwill.Width, actualHeight / TransverseTwill.Height) * layerScale,
                            SpriteEffects.None,
                            0
                        );
                    }
                }


                spriteBatch.End();
            }
            //绘制中间的球
            {
                //准备渲染状态
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                Effect shader = EffectLoader.AccretionDisk.Value;

                //计算实际渲染尺寸
                float actualWidth = Projectile.width * Projectile.scale;
                float actualHeight = Projectile.height * Projectile.scale;

                //世界空间到屏幕空间的变换矩阵
                //这里不需要复杂的矩阵变换，shader中会处理纹理坐标
                Matrix world = Matrix.Identity;
                Matrix view = Main.GameViewMatrix.TransformationMatrix;
                Matrix projection = Matrix.CreateOrthographicOffCenter(
                    0, Main.screenWidth,
                    Main.screenHeight, 0,
                    -1, 1);

                //组合矩阵
                Matrix finalMatrix = world * view * projection;

                shader.Parameters["transformMatrix"]?.SetValue(finalMatrix);
                shader.Parameters["uTime"]?.SetValue(3.6f);
                shader.Parameters["rotationSpeed"]?.SetValue(RotationSpeed);
                shader.Parameters["innerRadius"]?.SetValue(0.15f);
                shader.Parameters["outerRadius"]?.SetValue(0.85f);
                shader.Parameters["brightness"]?.SetValue(brightness);
                shader.Parameters["distortionStrength"]?.SetValue(distortionStrength);
                shader.Parameters["noiseTexture"]?.SetValue(VaultAsset.placeholder2.Value);

                //设置中心位置
                Vector2 screenCenter = Projectile.Center - Main.screenPosition;
                shader.Parameters["centerPos"]?.SetValue(screenCenter);

                //设置颜色
                shader.Parameters["innerColor"]?.SetValue(innerColor.ToVector4());
                shader.Parameters["midColor"]?.SetValue(midColor.ToVector4());
                shader.Parameters["outerColor"]?.SetValue(outerColor.ToVector4());

                //设置噪声纹理
                Main.graphics.GraphicsDevice.Textures[1] = TransverseTwill;
                Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

                shader.CurrentTechnique.Passes["AccretionDiskPass"].Apply();

                //计算绘制区域（屏幕空间）
                Vector2 drawPosition = Projectile.Center - Main.screenPosition + Projectile.rotation.ToRotationVector2() * 30 * Projectile.scale;
                Vector2 drawOrigin = new Vector2(actualWidth, actualHeight) * 0.5f;
                Rectangle sourceRect = new Rectangle(0, 0, (int)actualWidth, (int)actualHeight);

                for (int i = 0; i < 16; i++) {
                    //绘制一个简单的四边形，shader会处理所有的视觉效果
                    //使用TransverseTwill作为基础纹理，但实际效果由shader生成
                    spriteBatch.Draw(
                        TransverseTwill,
                        drawPosition,
                        null, //使用完整纹理
                        Color.White * (1f - Projectile.alpha / 255f),
                        Projectile.rotation + i * 0.1f,
                        TransverseTwill.Size() * 0.5f, //使用纹理中心作为原点
                        new Vector2(actualWidth / TransverseTwill.Width, actualHeight / TransverseTwill.Height) * (0.8f + i * 0.2f), //缩放到目标大小
                        SpriteEffects.None,
                        0
                    );
                }

                //恢复默认渲染状态
                spriteBatch.End();
            }
        }
    }
}
