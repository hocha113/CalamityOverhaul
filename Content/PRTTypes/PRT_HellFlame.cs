using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 地狱火焰粒子
    /// </summary>
    internal class PRT_HellFlame : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle3";

        private Color[] hellColors;
        private int timer;
        private float rotationSpeed;
        private int timeLeftMax;
        private float size;
        private float timeLife;
        private float flickerIntensity;
        private float distortionPhase;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> StarTexture = null;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Extra_193 = null;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            float[] origAI = [.. ai];
            ai = new float[4];
            for (int i = 0; i < origAI.Length; i++) {
                ai[i] = origAI[i];
            }

            //地狱风格的色彩方案：深红到亮橙到暗紫
            if (hellColors == null) {
                hellColors = new Color[5];
                hellColors[0] = new Color(255, 200, 80, 255);   //明亮橙黄（核心）
                hellColors[1] = new Color(255, 120, 30, 255);   //鲜艳橙色
                hellColors[2] = new Color(200, 40, 20, 255);    //深红色
                hellColors[3] = new Color(140, 20, 40, 255);    //暗红紫
                hellColors[4] = new Color(80, 10, 30, 255);     //极暗红（边缘）
            }

            //生命周期参数
            int minLife = ai[2] > 0 ? (int)ai[2] : 60;
            int maxLife = ai[3] > 0 ? (int)ai[3] : 120;
            timeLife = timer = Lifetime = Main.rand.Next(minLife, maxLife);
            timeLeftMax = Lifetime;

            //运动参数
            rotationSpeed = Main.rand.NextFloat(-0.15f, 0.15f);
            flickerIntensity = Main.rand.NextFloat(0.6f, 1.0f);
            distortionPhase = Main.rand.NextFloat(0f, MathHelper.TwoPi);

            //尺寸变化
            size = Main.rand.NextFloat(0.6f, 1.4f);

            //ai[0]: 行为模式 (0=向上飘，1=爆炸扩散，2=螺旋上升)
            //ai[1]: 特效强度
        }

        public override void AI() {
            float lifeRatio = timeLife / timeLeftMax;

            //复杂的透明度曲线：快速出现->持续->快速消失
            if (lifeRatio > 0.7f) {
                //淡入阶段
                Opacity = MathHelper.Lerp(0f, 1f, (1f - lifeRatio) / 0.3f);
            }
            else if (lifeRatio < 0.2f) {
                //淡出阶段
                Opacity = lifeRatio / 0.2f * flickerIntensity;
            }
            else {
                //持续阶段带闪烁
                float flicker = 0.85f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 15f + distortionPhase) * 0.15f;
                Opacity = flicker * flickerIntensity;
            }

            //根据行为模式应用不同的运动
            switch ((int)ai[0]) {
                case 0: //默认：向上飘散 + 扭曲
                    ApplyRisingMotion(lifeRatio);
                    break;

                case 1: //爆炸扩散
                    ApplyExplosionMotion(lifeRatio);
                    break;

                case 2: //螺旋上升
                    ApplySpiralMotion(lifeRatio);
                    break;

                case 3: //环绕旋转
                    ApplyOrbitMotion(lifeRatio);
                    break;
            }

            //旋转
            Rotation += rotationSpeed * (1f - lifeRatio * 0.5f);

            //尺寸变化：先膨胀后收缩
            float sizeCurve = (float)Math.Sin(lifeRatio * MathHelper.Pi);
            Scale = size * (0.5f + sizeCurve * 1.2f) * (ai[1] > 0 ? ai[1] : 1f) * 0.1f;

            timer--;
            timeLife--;
        }

        private void ApplyRisingMotion(float lifeRatio) {
            //热力上升 + 横向扭曲
            float sineWave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f + Position.X * 0.01f + distortionPhase);
            Vector2 distortion = new Vector2(sineWave * 0.8f, -1.2f);
            Velocity = Vector2.Lerp(Velocity, distortion, 0.08f);
            Velocity *= 0.98f;
        }

        private void ApplyExplosionMotion(float lifeRatio) {
            //爆炸扩散：初期快速远离，后期减速
            float speedCurve = (float)Math.Pow(1f - lifeRatio, 2);
            Velocity *= 0.96f + speedCurve * 0.04f;
        }

        private void ApplySpiralMotion(float lifeRatio) {
            //螺旋上升
            float angle = Rotation + timer * 0.1f;
            Vector2 spiral = new Vector2(
                (float)Math.Cos(angle) * 0.5f,
                -1.5f
            );
            Velocity = Vector2.Lerp(Velocity, spiral, 0.1f);
        }

        private void ApplyOrbitMotion(float lifeRatio) {
            //环绕中心点旋转
            float angle = timer * 0.08f + distortionPhase;
            Vector2 tangent = new Vector2(
                -(float)Math.Sin(angle),
                (float)Math.Cos(angle)
            ) * 2f;
            Velocity = Vector2.Lerp(Velocity, tangent, 0.15f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D mainTex = PRTLoader.PRT_IDToTexture[ID];
            Texture2D starTex = StarTexture?.Value;
            Texture2D glowTex = SoftGlow?.Value;
            Texture2D extraTex = Extra_193?.Value;

            if (mainTex == null) return false;

            float lifeRatio = timeLife / timeLeftMax;
            Vector2 drawPos = Position - Main.screenPosition;

            //多层颜色混合：创造深度感
            Color coreColor = GetBlendedColor(lifeRatio, 0f, 0.3f);//核心：亮橙
            Color midColor = GetBlendedColor(lifeRatio, 0.3f, 0.7f);//中层：深红
            Color edgeColor = GetBlendedColor(lifeRatio, 0.7f, 1f);//边缘：暗紫

            float finalOpacity = Opacity * (ai[1] > 0 ? Math.Min(ai[1], 2f) : 1f);

            //第1层：外层辉光（最大范围）
            if (glowTex != null) {
                spriteBatch.Draw(
                    glowTex,
                    drawPos,
                    new Rectangle(0, 0, 64, 64),
                    edgeColor * (finalOpacity * 0.4f),
                    Rotation * 0.5f,
                    new Vector2(32f, 32f),
                    Scale * 1.8f,
                    SpriteEffects.None,
                    0f
                );
            }

            //第2层：中层火焰（主体）
            spriteBatch.Draw(
                mainTex,
                drawPos,
                new Rectangle(0, 0, 64, 64),
                midColor * (finalOpacity * 0.8f),
                Rotation,
                new Vector2(32f, 32f),
                Scale * 1.2f,
                SpriteEffects.None,
                0f
            );

            //第3层：核心高亮（强烈光芒）
            spriteBatch.Draw(
                mainTex,
                drawPos,
                new Rectangle(0, 0, 64, 64),
                coreColor * (finalOpacity * 1.2f),
                Rotation * 1.5f,
                new Vector2(32f, 32f),
                Scale * 0.6f,
                SpriteEffects.None,
                0f
            );

            //第4层：星形闪光（核心爆发）
            if (starTex != null && lifeRatio > 0.5f) {
                float starPulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f + distortionPhase);
                float starIntensity = (lifeRatio - 0.5f) / 0.5f * (0.6f + starPulse * 0.4f);

                spriteBatch.Draw(
                    starTex,
                    drawPos,
                    null,
                    coreColor * (finalOpacity * starIntensity),
                    Rotation * 2f,
                    starTex.Size() / 2f,
                    Scale * 0.3f,
                    SpriteEffects.None,
                    0f
                );
            }

            //第5层：额外扭曲效果（可选）
            if (extraTex != null && ai[0] == 2) {
                float distortPhase = (float)Math.Sin(timer * 0.2f + distortionPhase);
                spriteBatch.Draw(
                    extraTex,
                    drawPos,
                    null,
                    midColor * (finalOpacity * 0.3f * Math.Abs(distortPhase)),
                    Rotation + distortPhase,
                    extraTex.Size() / 2f,
                    Scale * (1f + Math.Abs(distortPhase) * 0.5f),
                    SpriteEffects.None,
                    0f
                );
            }

            return false;
        }

        /// <summary>
        /// 在生命周期中混合多个颜色
        /// </summary>
        private Color GetBlendedColor(float lifeRatio, float rangeStart, float rangeEnd) {
            //将lifeRatio映射到颜色数组
            float normalizedLife = 1f - lifeRatio;
            int colorCount = hellColors.Length;
            float colorIndex = normalizedLife * (colorCount - 1);

            int index1 = (int)colorIndex;
            int index2 = Math.Min(index1 + 1, colorCount - 1);
            float blend = colorIndex - index1;

            //在指定范围内额外调整颜色
            if (normalizedLife >= rangeStart && normalizedLife <= rangeEnd) {
                float rangeBlend = (normalizedLife - rangeStart) / (rangeEnd - rangeStart);
                blend = MathHelper.Lerp(blend, rangeBlend, 0.5f);
            }

            return Color.Lerp(hellColors[index1], hellColors[index2], blend);
        }
    }
}
