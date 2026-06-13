using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.AcheronProtocols
{
    /// <summary>
    /// 银河系绘制：恒星生成、螺旋臂、银核发光、泰拉标记
    /// </summary>
    internal partial class GalacticCrisisRender
    {
        #region 银河系参数与数据

        private const int StarCount = 800;
        private const int GalaxyArmCount = 4;
        private const float GalaxyRadius = 240f;
        private const float GalaxyCoreRadius = 30f;
        private static readonly List<GalaxyStar> galaxyStars = [];
        private static float galaxyRotation;
        private static float galaxyRevealProgress;
        private static float terraBlinkTimer;

        //泰拉在旋臂上的位置参数
        private const float TerraRadialDistance = 0.45f;
        private const int TerraArmIndex = 2;
        private const float TerraAngleOffset = 0.15f;
        //泰拉的灭绝令状态
        private static bool terraExtinctionMarked;
        private static float terraExtinctionLerp;
        private static int terraExtinctionStage;
        private static float terraExtinctionStageTimer;

        private class GalaxyStar
        {
            public float ArmAngle;
            public float RadialDistance;
            public float AngleOffset;
            public float Brightness;
            public float BrightnessPhase;
            public float Size;
            public Color BaseColor;
            //灭绝令状态
            public bool ExtinctionMarked;
            public float ExtinctionLerp;
            //灭绝令闪烁阶段：0=变红中, 1=猛烈闪烁, 2=缩小消失
            public int ExtinctionStage;
            public float ExtinctionStageTimer;

            public Vector2 GetPosition(float rotation, float radius) {
                float r = RadialDistance * radius;
                float spiralTightness = 2.8f;
                float angle = ArmAngle + AngleOffset + spiralTightness * MathF.Log(MathF.Max(RadialDistance, 0.05f)) + rotation;
                return new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
            }
        }

        #endregion

        #region 初始化与清理

        private static void InitGalaxy() {
            galaxyRotation = 0f;
            galaxyRevealProgress = 0f;
            terraBlinkTimer = 0f;
            terraExtinctionMarked = false;
            terraExtinctionLerp = 0f;
            terraExtinctionStage = 0;
            terraExtinctionStageTimer = 0f;
            GenerateGalaxy();
        }

        private static void CleanupGalaxy() {
            galaxyStars.Clear();
        }

        private static void GenerateGalaxy() {
            galaxyStars.Clear();
            Color[] armColors = [
                new Color(180, 200, 255),
                new Color(200, 220, 255),
                new Color(160, 190, 255),
                new Color(220, 210, 255),
            ];

            for (int i = 0; i < StarCount; i++) {
                int armIndex = i % GalaxyArmCount;
                float armAngle = MathHelper.TwoPi * armIndex / GalaxyArmCount;
                float radial = MathF.Pow(Main.rand.NextFloat(), 0.6f);
                float angleJitter = Main.rand.NextFloat(-0.4f, 0.4f) * (0.3f + radial * 0.7f);

                float brightness = Main.rand.NextFloat(0.3f, 1f);
                brightness *= MathHelper.Lerp(1f, 0.5f, radial);
                if (radial < 0.15f) {
                    brightness = MathHelper.Lerp(brightness, 1f, 0.6f);
                }

                Color baseColor = armColors[armIndex];
                if (radial < 0.2f) {
                    baseColor = Color.Lerp(baseColor, new Color(255, 240, 200), (0.2f - radial) * 4f);
                }

                galaxyStars.Add(new GalaxyStar {
                    ArmAngle = armAngle,
                    RadialDistance = radial,
                    AngleOffset = angleJitter,
                    Brightness = brightness,
                    BrightnessPhase = Main.rand.NextFloat(MathHelper.TwoPi),
                    Size = Main.rand.NextFloat(0.8f, 2.5f) * MathHelper.Lerp(1f, 0.4f, radial),
                    BaseColor = baseColor
                });
            }
        }

        #endregion

        #region 逻辑更新

        /// <summary>
        /// 计算泰拉在旋臂上的屏幕坐标，使用与GalaxyStar.GetPosition相同的螺旋公式
        /// </summary>
        private static Vector2 GetTerraPosition(Vector2 center) {
            float r = TerraRadialDistance * GalaxyRadius;
            float spiralTightness = 2.8f;
            float armAngle = MathHelper.TwoPi * TerraArmIndex / GalaxyArmCount;
            float angle = armAngle + TerraAngleOffset + spiralTightness * MathF.Log(MathF.Max(TerraRadialDistance, 0.05f)) + galaxyRotation;
            return center + new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
        }

        private static void UpdateGalaxyLogic() {
            galaxyRotation += 0.0008f;
            terraBlinkTimer += 0.05f;
        }

        private static void UpdateGalaxyRevealPhase() {
            galaxyRevealProgress = MathF.Min(galaxyRevealProgress + 0.008f, 1f);
            phaseProgress = galaxyRevealProgress;
        }

        /// <summary>
        /// 强制将银河系设为已完全展示状态，用于从非初始阶段重新激活渲染器时
        /// </summary>
        internal static void ForceGalaxyRevealed() {
            galaxyRevealProgress = 1f;
        }

        #endregion

        #region 绘制

        private static void DrawGalaxy(SpriteBatch sb, Vector2 center, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            float reveal = VaultUtils.EaseOutCubic(galaxyRevealProgress);
            float galaxyAlpha = alpha * reveal;

            DrawGalaxyCore(sb, center, galaxyAlpha);

            Texture2D softGlow = CWRAsset.SoftGlow?.Value;

            foreach (var star in galaxyStars) {
                if (star.RadialDistance > reveal) continue;

                //灭绝令第三阶段（消失完毕）的恒星不绘制
                if (star.ExtinctionMarked && star.ExtinctionStage >= 2 && star.ExtinctionStageTimer >= ExtinctionFadeDuration) {
                    continue;
                }

                Vector2 pos = star.GetPosition(galaxyRotation, GalaxyRadius);
                Vector2 screenPos = center + pos;

                float flicker = MathF.Sin(globalTimer * 2f + star.BrightnessPhase) * 0.2f + 0.8f;
                float finalBrightness = star.Brightness * flicker * galaxyAlpha;
                float drawSize = star.Size;

                Color starColor = star.BaseColor;

                if (star.ExtinctionMarked) {
                    switch (star.ExtinctionStage) {
                        case 0:
                            //阶段0：变红中
                            Color extinctionRed = new Color(255, 50, 30);
                            starColor = Color.Lerp(starColor, extinctionRed, star.ExtinctionLerp);
                            finalBrightness *= MathHelper.Lerp(1f, 1.2f, star.ExtinctionLerp);
                            break;
                        case 1:
                            //阶段1：猛烈闪烁，亮度剧烈跳动
                            starColor = new Color(255, 50, 30);
                            float violentFlash = MathF.Sin(star.ExtinctionStageTimer * 30f + star.BrightnessPhase * 5f);
                            float flashIntensity = MathF.Abs(violentFlash);
                            finalBrightness *= 1.5f + flashIntensity * 2f;
                            drawSize *= 1f + flashIntensity * 0.8f;
                            break;
                        case 2:
                            //阶段2：缩小并消失
                            float fadeT = MathF.Min(star.ExtinctionStageTimer / ExtinctionFadeDuration, 1f);
                            starColor = Color.Lerp(new Color(255, 50, 30), new Color(80, 20, 15), fadeT);
                            float shrink = 1f - VaultUtils.EaseInCubic(fadeT);
                            drawSize *= shrink;
                            finalBrightness *= shrink;
                            if (drawSize < 0.05f) continue;
                            break;
                    }
                }

                starColor *= finalBrightness;

                sb.Draw(pixel, screenPos, new Rectangle(0, 0, 1, 1),
                    starColor, 0f, new Vector2(0.5f), new Vector2(drawSize), SpriteEffects.None, 0f);

                //光晕
                if (softGlow != null && (star.Brightness > 0.6f || (star.ExtinctionMarked && star.ExtinctionStage == 1))) {
                    Color glowColor = starColor * 0.3f;
                    glowColor.A = 0;
                    float glowScale = drawSize * 0.14f;
                    //灭绝闪烁阶段光晕更大
                    if (star.ExtinctionMarked && star.ExtinctionStage == 1) {
                        glowScale *= 2f;
                        glowColor = starColor * 0.5f;
                        glowColor.A = 0;
                    }
                    sb.Draw(softGlow, screenPos, null,
                        glowColor, 0f, new Vector2(softGlow.Width * 0.5f, softGlow.Height * 0.5f),
                        glowScale, SpriteEffects.None, 0f);
                }
            }
        }

        private static void DrawGalaxyCore(SpriteBatch sb, Vector2 center, float alpha) {
            Texture2D softGlow = CWRAsset.SoftGlow?.Value;

            if (softGlow != null) {
                Color coreColor = new Color(255, 240, 200);
                Vector2 glowOrigin = new(softGlow.Width * 0.5f, softGlow.Height * 0.5f);

                for (int i = 4; i >= 0; i--) {
                    float layerScale = GalaxyCoreRadius * (0.02f + i * 0.015f);
                    float layerAlpha = alpha * (0.35f - i * 0.05f);
                    float pulse = MathF.Sin(globalTimer * 1.5f + i * 0.5f) * 0.12f + 0.88f;
                    layerAlpha *= pulse;

                    Color layerColor = Color.Lerp(coreColor, new Color(180, 210, 255), i * 0.2f);
                    layerColor.A = 0;

                    sb.Draw(softGlow, center, null, layerColor * layerAlpha, 0f,
                        glowOrigin, layerScale, SpriteEffects.None, 0f);
                }
            }
            else {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                if (pixel == null) return;
                Color coreColor = new Color(255, 240, 200);
                for (int i = 5; i >= 0; i--) {
                    float layerRadius = GalaxyCoreRadius * (0.3f + i * 0.3f);
                    float layerAlpha = alpha * (0.4f - i * 0.05f);
                    float pulse = MathF.Sin(globalTimer * 1.5f + i * 0.5f) * 0.1f + 0.9f;
                    layerAlpha *= pulse;
                    Color layerColor = Color.Lerp(coreColor, new Color(180, 200, 255), i * 0.15f);
                    layerColor.A = 0;
                    int circleSegments = 16;
                    for (int j = 0; j < circleSegments; j++) {
                        float angle = MathHelper.TwoPi * j / circleSegments;
                        Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * layerRadius * 0.5f;
                        sb.Draw(pixel, center + offset, new Rectangle(0, 0, 1, 1),
                            layerColor * layerAlpha, angle, new Vector2(0.5f),
                            new Vector2(layerRadius * 0.8f, layerRadius * 0.3f), SpriteEffects.None, 0f);
                    }
                }
            }
        }

        private static void DrawTerraMarker(SpriteBatch sb, Vector2 center, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            //泰拉已被灭绝令完全摧毁，不再绘制
            if (terraExtinctionMarked && terraExtinctionStage >= 2 && terraExtinctionStageTimer >= ExtinctionFadeDuration) {
                return;
            }

            //泰拉在旋臂上跟随银河系旋转
            Vector2 terraPos = GetTerraPosition(center);
            float blink = MathF.Sin(terraBlinkTimer) * 0.3f + 0.7f;
            float markerAlpha = alpha * blink;

            Color terraColor = new Color(100, 255, 120);
            float drawScale = 1f;

            //灭绝令对泰拉的三阶段效果
            if (terraExtinctionMarked) {
                switch (terraExtinctionStage) {
                    case 0:
                        terraColor = Color.Lerp(new Color(100, 255, 120), new Color(255, 60, 40), terraExtinctionLerp);
                        break;
                    case 1:
                        terraColor = new Color(255, 60, 40);
                        float violentFlash = MathF.Abs(MathF.Sin(terraExtinctionStageTimer * 30f));
                        markerAlpha *= 1.5f + violentFlash * 2f;
                        drawScale = 1f + violentFlash * 0.6f;
                        break;
                    case 2:
                        float fadeT = MathF.Min(terraExtinctionStageTimer / ExtinctionFadeDuration, 1f);
                        terraColor = Color.Lerp(new Color(255, 60, 40), new Color(80, 20, 15), fadeT);
                        drawScale = 1f - VaultUtils.EaseInCubic(fadeT);
                        markerAlpha *= drawScale;
                        if (drawScale < 0.05f) return;
                        break;
                }
            }
            else if (extinctionProgress > 0.3f) {
                //波纹逼近但未被标记时，变色警告
                terraColor = Color.Lerp(new Color(100, 255, 120), new Color(255, 60, 40),
                    MathF.Sin(extinctionFlashTimer * 2f) * 0.5f + 0.5f);
            }

            Texture2D softGlow = CWRAsset.SoftGlow?.Value;

            if (softGlow != null) {
                Vector2 glowOrigin = new(softGlow.Width * 0.5f, softGlow.Height * 0.5f);

                //外层呼吸光环
                float ringPulse = (MathF.Sin(terraBlinkTimer * 0.7f) + 1f) * 0.5f;
                Color ringGlow = terraColor;
                ringGlow.A = 0;
                float ringScale = (0.22f + ringPulse * 0.12f) * drawScale;
                sb.Draw(softGlow, terraPos, null,
                    ringGlow * (markerAlpha * 0.3f * (1f - ringPulse * 0.3f)),
                    0f, glowOrigin, ringScale, SpriteEffects.None, 0f);

                //内核亮点
                Color coreGlow = terraColor;
                coreGlow.A = 0;
                sb.Draw(softGlow, terraPos, null,
                    coreGlow * (markerAlpha * 0.7f),
                    0f, glowOrigin, 0.08f * drawScale, SpriteEffects.None, 0f);

                //灭绝令闪烁阶段叠加红色大光斑
                if (terraExtinctionMarked && terraExtinctionStage == 1) {
                    float dangerPulse = MathF.Sin(extinctionFlashTimer * 3f) * 0.4f + 0.6f;
                    Color dangerGlow = new Color(255, 40, 20);
                    dangerGlow.A = 0;
                    sb.Draw(softGlow, terraPos, null,
                        dangerGlow * (alpha * 0.4f * dangerPulse),
                        0f, glowOrigin, 0.4f * drawScale, SpriteEffects.None, 0f);
                }
            }
            else {
                sb.Draw(pixel, terraPos, new Rectangle(0, 0, 1, 1),
                    terraColor * markerAlpha, 0f, new Vector2(0.5f),
                    new Vector2(4f * drawScale), SpriteEffects.None, 0f);
            }

            //标注文字"TERRA"（闪烁阶段不绘制以减少杂乱）
            if (!terraExtinctionMarked || terraExtinctionStage < 2) {
                float textAlpha = alpha * 0.7f;
                Utils.DrawBorderString(sb, MarkerTerra.Value, terraPos + new Vector2(12, -10),
                    terraColor * textAlpha, 0.45f);
            }
        }

        #endregion
    }
}
