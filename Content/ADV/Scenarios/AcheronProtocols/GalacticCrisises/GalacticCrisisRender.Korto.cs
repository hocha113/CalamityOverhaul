using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.AcheronProtocols
{
    /// <summary>
    /// 科尔托星系：银河系放大聚焦 → 标记科尔托 → 切换为行星链正视图 → 标记第三行星
    /// </summary>
    internal partial class GalacticCrisisRender
    {
        #region 科尔托参数与数据

        //科尔托星系在银河系中的位置（旋臂3，径向0.7）
        private const float KortoRadialDistance = 0.7f;
        private const int KortoArmIndex = 3;
        private const float KortoAngleOffset = -0.1f;

        //缩放聚焦阶段
        private static float kortoZoomProgress;
        private static float kortoZoomScale;
        private static Vector2 kortoZoomOffset;
        private static float kortoMarkerAlpha;

        //行星视图阶段
        private static float kortoPlanetViewProgress;
        private static float kortoPlanetTransition;
        private const int KortoPlanetCount = 6;
        private static float kortoPlanetOrbitTimer;

        //科尔托星系恒星（用于行星视图中心）
        private static readonly Color KortoStarColor = new(255, 200, 140);
        //第三行星标记
        private static float kortoTargetBlinkTimer;

        #endregion

        #region 初始化与清理

        private static void InitKorto() {
            kortoZoomProgress = 0f;
            kortoZoomScale = 1f;
            kortoZoomOffset = Vector2.Zero;
            kortoMarkerAlpha = 0f;
            kortoPlanetViewProgress = 0f;
            kortoPlanetTransition = 0f;
            kortoPlanetOrbitTimer = 0f;
            kortoTargetBlinkTimer = 0f;
        }

        private static void CleanupKorto() {
            kortoZoomProgress = 0f;
            kortoZoomScale = 1f;
            kortoZoomOffset = Vector2.Zero;
        }

        #endregion

        #region 逻辑更新

        /// <summary>

        /// 科尔托聚焦阶段：银河系不断放大，镜头平移到科尔托位置

        /// </summary>
        private static void UpdateKortoZoomPhase() {
            kortoZoomProgress = MathF.Min(kortoZoomProgress + 0.006f, 1f);
            phaseProgress = kortoZoomProgress;

            float ease = VaultUtils.EaseInOutCubic(kortoZoomProgress);

            //缩放：从1x到5x
            kortoZoomScale = MathHelper.Lerp(1f, 5f, ease);

            //计算科尔托在银河中的原始位置偏移
            float r = KortoRadialDistance * GalaxyRadius;
            float spiralTightness = 2.8f;
            float armAngle = MathHelper.TwoPi * KortoArmIndex / GalaxyArmCount;
            float angle = armAngle + KortoAngleOffset + spiralTightness * MathF.Log(MathF.Max(KortoRadialDistance, 0.05f)) + galaxyRotation;
            Vector2 kortoLocalPos = new(MathF.Cos(angle) * r, MathF.Sin(angle) * r);

            //镜头偏移：将科尔托位置移到画面中心
            kortoZoomOffset = -kortoLocalPos * ease;

            //科尔托标记在缩放超过60%后开始出现
            float markerFade = MathHelper.Clamp((kortoZoomProgress - 0.6f) / 0.3f, 0f, 1f);
            kortoMarkerAlpha = VaultUtils.EaseOutCubic(markerFade);

            //缩放过程中逐渐降低glitch
            glitchIntensity = MathHelper.Lerp(glitchIntensity, 0.01f, 0.05f);
        }

        /// <summary>

        /// 科尔托行星视图阶段：从银河俯视图过渡到行星链正视图

        /// </summary>
        private static void UpdateKortoPlanetViewPhase() {
            kortoPlanetViewProgress = MathF.Min(kortoPlanetViewProgress + 0.005f, 1f);
            phaseProgress = kortoPlanetViewProgress;

            //过渡：银河淡出，行星视图淡入
            kortoPlanetTransition = MathF.Min(kortoPlanetTransition + 0.02f, 1f);

            //行星轨道运动
            kortoPlanetOrbitTimer += 0.008f;

            //目标标记闪烁
            kortoTargetBlinkTimer += 0.06f;

            glitchIntensity = MathHelper.Lerp(glitchIntensity, 0.005f, 0.05f);
        }

        #endregion

        #region 绘制

        /// <summary>

        /// 在KortoZoom阶段绘制放大中的银河系 + 科尔托标记

        /// </summary>
        private static void DrawKortoZoomGalaxy(SpriteBatch sb, Vector2 center, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            //应用缩放和偏移
            Vector2 zoomCenter = center + kortoZoomOffset * kortoZoomScale;

            float reveal = VaultUtils.EaseOutCubic(galaxyRevealProgress);
            float galaxyAlpha = alpha * reveal;

            //缩放后的银河核心
            DrawGalaxyCoreZoomed(sb, zoomCenter, galaxyAlpha);

            Texture2D softGlow = CWRAsset.SoftGlow?.Value;

            //绘制缩放后的恒星
            foreach (var star in galaxyStars) {
                if (star.RadialDistance > reveal) continue;
                if (star.ExtinctionMarked && star.ExtinctionStage >= 2 && star.ExtinctionStageTimer >= ExtinctionFadeDuration) {
                    continue;
                }

                Vector2 pos = star.GetPosition(galaxyRotation, GalaxyRadius);
                Vector2 screenPos = zoomCenter + pos * kortoZoomScale;

                //只绘制在合理屏幕范围内的恒星
                Rectangle panelRect = GetPanelRect();
                if (screenPos.X < panelRect.X - 20 || screenPos.X > panelRect.Right + 20 ||
                    screenPos.Y < panelRect.Y - 20 || screenPos.Y > panelRect.Bottom + 20) continue;

                float flicker = MathF.Sin(globalTimer * 2f + star.BrightnessPhase) * 0.2f + 0.8f;
                float finalBrightness = star.Brightness * flicker * galaxyAlpha;
                float drawSize = star.Size * MathF.Min(kortoZoomScale * 0.6f, 2.5f);

                Color starColor = star.BaseColor;
                //灭绝后恒星保持暗色
                if (star.ExtinctionMarked && star.ExtinctionStage >= 2) {
                    float fadeT = MathF.Min(star.ExtinctionStageTimer / ExtinctionFadeDuration, 1f);
                    float shrink = 1f - VaultUtils.EaseInCubic(fadeT);
                    drawSize *= shrink;
                    finalBrightness *= shrink;
                    if (drawSize < 0.05f) continue;
                }
                starColor *= finalBrightness;

                sb.Draw(pixel, screenPos, new Rectangle(0, 0, 1, 1),
                    starColor, 0f, new Vector2(0.5f), new Vector2(drawSize), SpriteEffects.None, 0f);

                if (softGlow != null && star.Brightness > 0.7f) {
                    Color glowColor = starColor * 0.25f;
                    glowColor.A = 0;
                    sb.Draw(softGlow, screenPos, null, glowColor, 0f,
                        new Vector2(softGlow.Width * 0.5f, softGlow.Height * 0.5f),
                        drawSize * 0.12f, SpriteEffects.None, 0f);
                }
            }

            //绘制科尔托标记
            if (kortoMarkerAlpha > 0.01f) {
                DrawKortoMarker(sb, zoomCenter, alpha * kortoMarkerAlpha);
            }
        }

        private static void DrawGalaxyCoreZoomed(SpriteBatch sb, Vector2 center, float alpha) {
            Texture2D softGlow = CWRAsset.SoftGlow?.Value;
            if (softGlow == null) return;

            Color coreColor = new Color(255, 240, 200);
            Vector2 glowOrigin = new(softGlow.Width * 0.5f, softGlow.Height * 0.5f);

            for (int i = 3; i >= 0; i--) {
                float layerScale = GalaxyCoreRadius * (0.02f + i * 0.015f) * MathF.Min(kortoZoomScale, 3f);
                float layerAlpha = alpha * (0.3f - i * 0.05f);
                float pulse = MathF.Sin(globalTimer * 1.5f + i * 0.5f) * 0.12f + 0.88f;
                layerAlpha *= pulse;

                Color layerColor = Color.Lerp(coreColor, new Color(180, 210, 255), i * 0.2f);
                layerColor.A = 0;

                sb.Draw(softGlow, center, null, layerColor * layerAlpha, 0f,
                    glowOrigin, layerScale, SpriteEffects.None, 0f);
            }
        }

        private static void DrawKortoMarker(SpriteBatch sb, Vector2 zoomCenter, float alpha) {
            float r = KortoRadialDistance * GalaxyRadius;
            float spiralTightness = 2.8f;
            float armAngle = MathHelper.TwoPi * KortoArmIndex / GalaxyArmCount;
            float angle = armAngle + KortoAngleOffset + spiralTightness * MathF.Log(MathF.Max(KortoRadialDistance, 0.05f)) + galaxyRotation;
            Vector2 kortoPos = zoomCenter + new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r) * kortoZoomScale;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Texture2D softGlow = CWRAsset.SoftGlow?.Value;
            if (pixel == null) return;

            Color markerColor = new Color(255, 180, 60);
            float pulse = MathF.Sin(globalTimer * 3f) * 0.3f + 0.7f;

            //瞄准框四角
            float frameSize = 18f + MathF.Sin(globalTimer * 2f) * 3f;
            float cornerLen = 8f;
            Color frameColor = markerColor * (alpha * 0.9f * pulse);

            //左上
            sb.Draw(pixel, kortoPos + new Vector2(-frameSize, -frameSize), new Rectangle(0, 0, 1, 1),
                frameColor, 0f, Vector2.Zero, new Vector2(cornerLen, 1.5f), SpriteEffects.None, 0f);
            sb.Draw(pixel, kortoPos + new Vector2(-frameSize, -frameSize), new Rectangle(0, 0, 1, 1),
                frameColor, 0f, Vector2.Zero, new Vector2(1.5f, cornerLen), SpriteEffects.None, 0f);
            //右上
            sb.Draw(pixel, kortoPos + new Vector2(frameSize - cornerLen, -frameSize), new Rectangle(0, 0, 1, 1),
                frameColor, 0f, Vector2.Zero, new Vector2(cornerLen, 1.5f), SpriteEffects.None, 0f);
            sb.Draw(pixel, kortoPos + new Vector2(frameSize - 1.5f, -frameSize), new Rectangle(0, 0, 1, 1),
                frameColor, 0f, Vector2.Zero, new Vector2(1.5f, cornerLen), SpriteEffects.None, 0f);
            //左下
            sb.Draw(pixel, kortoPos + new Vector2(-frameSize, frameSize - 1.5f), new Rectangle(0, 0, 1, 1),
                frameColor, 0f, Vector2.Zero, new Vector2(cornerLen, 1.5f), SpriteEffects.None, 0f);
            sb.Draw(pixel, kortoPos + new Vector2(-frameSize, frameSize - cornerLen), new Rectangle(0, 0, 1, 1),
                frameColor, 0f, Vector2.Zero, new Vector2(1.5f, cornerLen), SpriteEffects.None, 0f);
            //右下
            sb.Draw(pixel, kortoPos + new Vector2(frameSize - cornerLen, frameSize - 1.5f), new Rectangle(0, 0, 1, 1),
                frameColor, 0f, Vector2.Zero, new Vector2(cornerLen, 1.5f), SpriteEffects.None, 0f);
            sb.Draw(pixel, kortoPos + new Vector2(frameSize - 1.5f, frameSize - cornerLen), new Rectangle(0, 0, 1, 1),
                frameColor, 0f, Vector2.Zero, new Vector2(1.5f, cornerLen), SpriteEffects.None, 0f);

            //中心光点
            if (softGlow != null) {
                Vector2 glowOrigin = new(softGlow.Width * 0.5f, softGlow.Height * 0.5f);
                Color centerGlow = markerColor;
                centerGlow.A = 0;
                sb.Draw(softGlow, kortoPos, null, centerGlow * (alpha * 0.6f * pulse), 0f,
                    glowOrigin, 0.12f, SpriteEffects.None, 0f);

                //外圈脉冲
                float ringPulse = (MathF.Sin(globalTimer * 1.5f) + 1f) * 0.5f;
                sb.Draw(softGlow, kortoPos, null,
                    centerGlow * (alpha * 0.2f * (1f - ringPulse)),
                    0f, glowOrigin, 0.25f + ringPulse * 0.1f, SpriteEffects.None, 0f);
            }

            //标注文字
            Color textColor = markerColor * alpha;
            Utils.DrawBorderString(sb, KortoSystemLabel.Value, kortoPos + new Vector2(frameSize + 6, -8),
                textColor, 0.42f);
            Utils.DrawBorderString(sb, KortoTargetLocked.Value, kortoPos + new Vector2(frameSize + 6, 6),
                new Color(255, 100, 60) * (alpha * pulse), 0.32f);
        }

        private static void DrawKortoPlanetView(SpriteBatch sb, Vector2 center, float alpha) {
            float viewAlpha = alpha * VaultUtils.EaseOutCubic(kortoPlanetTransition);
            if (viewAlpha < 0.01f) return;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Texture2D softGlow = CWRAsset.SoftGlow?.Value;
            if (pixel == null) return;

            Rectangle panelRect = GetPanelRect();

            //行星视图中心偏左（恒星位置）
            Vector2 starPos = new(panelRect.X + panelRect.Width * 0.08f, center.Y);

            //绘制恒星
            DrawKortoStar(sb, starPos, viewAlpha);

            //绘制行星链（水平分布，从左到右距离递增）
            float planetSpacing = (panelRect.Width * 0.85f) / (KortoPlanetCount + 1);

            for (int i = 0; i < KortoPlanetCount; i++) {
                float planetX = starPos.X + planetSpacing * (i + 1);
                //每颗行星有轻微垂直波动
                float yOffset = MathF.Sin(kortoPlanetOrbitTimer * (1.5f - i * 0.15f) + i * 1.7f) * (12f + i * 3f);
                Vector2 planetPos = new(planetX, center.Y + yOffset);

                bool isTarget = (i == 2); //第三行星（index 2）
                DrawKortoPlanet(sb, planetPos, i, isTarget, viewAlpha);

                //轨道线（虚线）
                DrawOrbitLine(sb, starPos, planetPos, viewAlpha * 0.25f);
            }

            //顶部标题
            DrawKortoPlanetViewHeader(sb, panelRect, viewAlpha);
        }

        private static void DrawKortoStar(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D softGlow = CWRAsset.SoftGlow?.Value;
            Texture2D pixel = VaultAsset.placeholder2.Value;

            if (softGlow != null) {
                Vector2 glowOrigin = new(softGlow.Width * 0.5f, softGlow.Height * 0.5f);
                float pulse = MathF.Sin(globalTimer * 1.2f) * 0.1f + 0.9f;

                //最外层柔和弥散光晕
                Color diffuseGlow = new Color(255, 180, 80);
                diffuseGlow.A = 0;
                sb.Draw(softGlow, pos, null, diffuseGlow * (alpha * 0.15f), 0f,
                    glowOrigin, 1.2f, SpriteEffects.None, 0f);

                //外层光晕
                Color outerGlow = new Color(255, 200, 120);
                outerGlow.A = 0;
                sb.Draw(softGlow, pos, null, outerGlow * (alpha * 0.35f * pulse), 0f,
                    glowOrigin, 0.6f, SpriteEffects.None, 0f);

                //中层暖光
                Color midGlow = new Color(255, 230, 170);
                midGlow.A = 0;
                sb.Draw(softGlow, pos, null, midGlow * (alpha * 0.55f * pulse), 0f,
                    glowOrigin, 0.25f, SpriteEffects.None, 0f);

                //核心
                Color coreGlow = new Color(255, 245, 220);
                coreGlow.A = 0;
                sb.Draw(softGlow, pos, null, coreGlow * (alpha * 0.85f), 0f,
                    glowOrigin, 0.1f, SpriteEffects.None, 0f);

                //亮点
                sb.Draw(softGlow, pos, null, Color.White * (alpha * 0.6f), 0f,
                    glowOrigin, 0.04f, SpriteEffects.None, 0f);

                //光芒十字射线
                Color rayColor = new Color(255, 220, 140);
                rayColor.A = 0;
                float rayAlpha = alpha * 0.15f * pulse;
                for (int i = 0; i < 4; i++) {
                    float angle = i * MathHelper.PiOver2 + globalTimer * 0.1f;
                    sb.Draw(softGlow, pos, null, rayColor * rayAlpha, angle,
                        glowOrigin, new Vector2(0.8f, 0.03f), SpriteEffects.None, 0f);
                }
            }
            else if (pixel != null) {
                //像素回退：绘制恒星圆
                DrawFilledCirclePixel(sb, pixel, pos, 10f, KortoStarColor * alpha);
                DrawFilledCirclePixel(sb, pixel, pos, 7f, new Color(255, 240, 200) * alpha);
            }

            //标注文字
            Color labelColor = new Color(255, 200, 120) * (alpha * 0.6f);
            Utils.DrawBorderString(sb, KortoStarLabel.Value, pos + new Vector2(-15, 22), labelColor, 0.35f);
        }

        private static void DrawKortoPlanet(SpriteBatch sb, Vector2 pos, int index, bool isTarget, float alpha) {
            Texture2D softGlow = CWRAsset.SoftGlow?.Value;
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            //行星基础参数
            float[] planetRadii = [8f, 12f, 18f, 28f, 15f, 9f];
            Color[] planetColors = [
                new Color(180, 160, 140),  //I: 水星型岩石行星
                new Color(210, 185, 130),  //II: 沙漠行星
                new Color(80, 145, 190),   //III: 目标类地行星
                new Color(220, 190, 130),  //IV: 气态巨行星
                new Color(140, 155, 190),  //V: 冰巨行星
                new Color(150, 140, 140),  //VI: 矮行星
            ];
            //行星暗面颜色（朝恒星背面）
            Color[] shadowColors = [
                new Color(60, 50, 45),
                new Color(80, 65, 40),
                new Color(25, 50, 70),
                new Color(90, 70, 45),
                new Color(50, 55, 70),
                new Color(55, 50, 50),
            ];
            //大气层颜色
            Color[] atmosColors = [
                Color.Transparent,
                new Color(220, 180, 100),
                new Color(100, 180, 255),
                new Color(255, 210, 130),
                new Color(150, 180, 220),
                Color.Transparent,
            ];
            //自转速度
            float[] rotSpeeds = [0.8f, 0.5f, 0.6f, 0.3f, 0.35f, 0.7f];

            float radius = planetRadii[index];
            Color baseColor = planetColors[index];
            Color shadowColor = shadowColors[index];
            Color atmosColor = atmosColors[index];
            float rotSpeed = rotSpeeds[index];
            float selfRotation = kortoPlanetOrbitTimer * rotSpeed + index * 2.3f;

            //=== 绘制行星本体（像素逼近圆）===
            DrawPlanetSphere(sb, pixel, pos, radius, baseColor, shadowColor, selfRotation, alpha, index);

            //=== 大气层光晕 ===
            if (softGlow != null && atmosColor != Color.Transparent) {
                Vector2 glowOrigin = new(softGlow.Width * 0.5f, softGlow.Height * 0.5f);
                Color atmos = atmosColor;
                atmos.A = 0;
                float atmosScale = radius * 0.028f;
                float atmosPulse = MathF.Sin(globalTimer * 1.5f + index * 0.8f) * 0.1f + 0.9f;
                sb.Draw(softGlow, pos, null, atmos * (alpha * 0.2f * atmosPulse), 0f,
                    glowOrigin, atmosScale, SpriteEffects.None, 0f);
            }

            //=== 气态巨行星环系统（仅IV号行星）===
            if (index == 3) {
                DrawPlanetRing(sb, pixel, pos, radius, alpha);
            }

            //=== 目标行星特殊标记 ===
            if (isTarget) {
                DrawTargetPlanetEffects(sb, pos, radius, alpha);
            }
            else {
                //普通行星编号（罗马数字）
                string[] romanNumerals = ["I", "II", "III", "IV", "V", "VI"];
                string label = romanNumerals[index];
                Color labelColor = new Color(150, 180, 220) * (alpha * 0.5f);
                Vector2 labelSize = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(label) * 0.32f;
                Utils.DrawBorderString(sb, label, pos + new Vector2(-labelSize.X * 0.5f, -radius - 14),
                    labelColor, 0.32f);
            }
        }

        /// <summary>

        /// 用像素逼近绘制行星球体，包含：明暗面、明暗分界线、表面细节纹理

        /// </summary>
        private static void DrawPlanetSphere(SpriteBatch sb, Texture2D pixel, Vector2 center,
            float radius, Color lightColor, Color darkColor, float selfRotation, float alpha, int planetIndex) {
            //光源方向（从左侧恒星照射）
            Vector2 lightDir = new(-1f, -0.3f);
            if (lightDir != Vector2.Zero) lightDir = Vector2.Normalize(lightDir);

            int r = (int)MathF.Ceiling(radius);

            for (int y = -r; y <= r; y++) {
                for (int x = -r; x <= r; x++) {
                    float dist = MathF.Sqrt(x * x + y * y);
                    if (dist > radius) continue;

                    //球面法线
                    float nx = x / radius;
                    float ny = y / radius;
                    float nz = MathF.Sqrt(MathF.Max(0f, 1f - nx * nx - ny * ny));

                    //光照计算（Lambert漫反射）
                    float ndotl = -(lightDir.X * nx + lightDir.Y * ny) * 0.5f + nz * 0.5f;
                    ndotl = MathHelper.Clamp(ndotl, 0f, 1f);

                    //明暗分界线（terminator）加宽渐变
                    float terminatorSharpness = 0.35f;
                    float shade = MathHelper.Clamp((ndotl - 0.3f) / terminatorSharpness, 0f, 1f);

                    Color pixelColor = Color.Lerp(darkColor, lightColor, shade);

                    //表面细节纹理（基于球面UV + 自转偏移）
                    float u = MathF.Atan2(ny, nx + nz * 0.3f) + selfRotation;
                    float v = ny;

                    float surfaceNoise = GetSurfaceNoise(u, v, planetIndex);
                    pixelColor = Color.Lerp(pixelColor, pixelColor * (0.7f + surfaceNoise * 0.6f), 0.5f);

                    //边缘大气散射（limb darkening / brightening）
                    float edgeFactor = 1f - nz;
                    float limbDarken = MathF.Pow(edgeFactor, 2f) * 0.3f;
                    pixelColor = Color.Lerp(pixelColor, darkColor, limbDarken);

                    //高光斑点（朝光面边缘）
                    if (ndotl > 0.7f && edgeFactor > 0.4f && edgeFactor < 0.8f) {
                        float specular = (ndotl - 0.7f) / 0.3f * (edgeFactor - 0.4f) / 0.4f;
                        pixelColor = Color.Lerp(pixelColor, Color.White, specular * 0.15f);
                    }

                    sb.Draw(pixel, center + new Vector2(x, y), new Rectangle(0, 0, 1, 1),
                        pixelColor * alpha, 0f, new Vector2(0.5f), 1f, SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>

        /// 生成行星表面伪噪声纹理（不使用Random，纯数学确定性）

        /// </summary>
        private static float GetSurfaceNoise(float u, float v, int planetIndex) {
            //基于行星索引给不同的纹理特征
            float freq1 = 3f + planetIndex * 0.7f;
            float freq2 = 7f + planetIndex * 1.3f;

            float noise = MathF.Sin(u * freq1 + v * 2f) * 0.3f
                        + MathF.Sin(u * freq2 - v * 3f + 1.5f) * 0.2f
                        + MathF.Cos(u * 5f + v * freq1) * 0.15f;

            //气态巨行星：水平条带纹理
            if (planetIndex == 3) {
                float bands = MathF.Sin(v * 12f) * 0.35f + MathF.Sin(v * 25f + u * 2f) * 0.15f;
                noise = bands;
            }
            //冰巨行星：较柔和的条带
            if (planetIndex == 4) {
                float bands = MathF.Sin(v * 8f) * 0.25f + MathF.Cos(v * 15f + u) * 0.1f;
                noise = bands;
            }
            //类地行星（目标）：大陆纹理
            if (planetIndex == 2) {
                float continent = MathF.Sin(u * 3f + 0.5f) * MathF.Cos(v * 4f + 1f);
                continent = MathHelper.Clamp(continent, -0.5f, 0.5f);
                noise = continent * 0.4f + MathF.Sin(u * 8f + v * 6f) * 0.1f;
            }

            return MathHelper.Clamp(noise * 0.5f + 0.5f, 0f, 1f);
        }

        private static void DrawPlanetRing(SpriteBatch sb, Texture2D pixel, Vector2 center, float planetRadius, float alpha) {
            float innerR = planetRadius * 1.4f;
            float outerR = planetRadius * 2.2f;
            Color ringColor1 = new Color(200, 180, 140);
            Color ringColor2 = new Color(170, 150, 110);
            Color ringGap = new Color(100, 85, 65);

            //环的倾斜（用y缩放模拟倾角）
            float tilt = 0.3f;
            int segments = 60;

            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments;
                float nextAngle = MathHelper.TwoPi * (i + 1) / segments;

                //只绘制行星后面和前面的环段（跳过被行星遮挡的部分）
                float sinA = MathF.Sin(angle);

                //多层环
                for (int ring = 0; ring < 3; ring++) {
                    float rInner = innerR + (outerR - innerR) * ring / 3f;
                    float rOuter = innerR + (outerR - innerR) * (ring + 1) / 3f;
                    float rMid = (rInner + rOuter) * 0.5f;

                    Vector2 p1 = center + new Vector2(MathF.Cos(angle) * rMid, MathF.Sin(angle) * rMid * tilt);
                    Vector2 p2 = center + new Vector2(MathF.Cos(nextAngle) * rMid, MathF.Sin(nextAngle) * rMid * tilt);

                    //被行星遮挡的部分降低透明度
                    float occlude = 1f;
                    if (sinA > -0.2f && sinA < 0.2f) {
                        float absX = MathF.Abs(MathF.Cos(angle) * rMid);
                        if (absX < planetRadius * 0.9f) {
                            occlude = 0.15f;
                        }
                    }
                    //行星前面的环段（sin > 0）半透明
                    if (sinA > 0) {
                        occlude *= 0.6f;
                    }

                    Color rColor = ring == 1 ? ringGap : (ring == 0 ? ringColor1 : ringColor2);
                    float ringAlpha = alpha * 0.5f * occlude * (ring == 1 ? 0.4f : 0.7f);

                    Vector2 diff = p2 - p1;
                    float len = diff.Length();
                    if (len < 0.5f) continue;
                    float segAngle = MathF.Atan2(diff.Y, diff.X);
                    float thickness = (rOuter - rInner) * tilt * 0.4f;

                    sb.Draw(pixel, p1, new Rectangle(0, 0, 1, 1),
                        rColor * ringAlpha, segAngle, Vector2.Zero,
                        new Vector2(len, MathF.Max(thickness, 0.8f)), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>

        /// 目标行星（III号）的特殊标记效果

        /// </summary>
        private static void DrawTargetPlanetEffects(SpriteBatch sb, Vector2 pos, float radius, float alpha) {
            Texture2D softGlow = CWRAsset.SoftGlow?.Value;
            float targetPulse = MathF.Sin(kortoTargetBlinkTimer) * 0.3f + 0.7f;

            //红色警告光环
            if (softGlow != null) {
                Vector2 glowOrigin = new(softGlow.Width * 0.5f, softGlow.Height * 0.5f);

                //外圈脉动危险光环
                Color dangerGlow = new Color(255, 60, 30);
                dangerGlow.A = 0;
                float ringScale = radius * 0.035f + MathF.Sin(kortoTargetBlinkTimer * 0.8f) * radius * 0.006f;
                sb.Draw(softGlow, pos, null, dangerGlow * (alpha * 0.3f * targetPulse), 0f,
                    glowOrigin, ringScale, SpriteEffects.None, 0f);
            }

            //瞄准框
            DrawTargetReticle(sb, pos, alpha * targetPulse);

            //名称标注
            Color targetTextColor = new Color(255, 80, 40) * (alpha * targetPulse);
            Utils.DrawBorderString(sb, KortoIIILabel.Value, pos + new Vector2(-26, -radius - 26),
                targetTextColor, 0.5f);

            //副标题
            float subtitleAlpha = alpha * MathF.Max(0f, (kortoPlanetViewProgress - 0.4f) * 2.5f);
            if (subtitleAlpha > 0.01f) {
                Color subColor = new Color(255, 200, 60) * subtitleAlpha;
                Utils.DrawBorderString(sb, KortoPrimaryObjective.Value, pos + new Vector2(-58, radius + 16),
                    subColor, 0.38f);
            }

            //行星数据标签（逐步显示）
            float dataAlpha = alpha * MathF.Max(0f, (kortoPlanetViewProgress - 0.6f) * 2.5f);
            if (dataAlpha > 0.01f) {
                Color dataColor = new Color(180, 200, 220) * dataAlpha;
                float dataY = radius + 34;
                Utils.DrawBorderString(sb, KortoClassTerrestrial.Value, pos + new Vector2(-48, dataY),
                    dataColor * 0.7f, 0.3f);
                Utils.DrawBorderString(sb, KortoThreatCritical.Value, pos + new Vector2(-44, dataY + 14),
                    new Color(255, 100, 60) * dataAlpha * 0.8f, 0.3f);
            }
        }

        /// <summary>用像素逼近绘制实心圆（辅助方法</summary>
        private static void DrawFilledCirclePixel(SpriteBatch sb, Texture2D pixel, Vector2 center, float radius, Color color) {
            int r = (int)MathF.Ceiling(radius);
            for (int y = -r; y <= r; y++) {
                //计算该行的水平范围
                float halfWidth = MathF.Sqrt(radius * radius - y * y);
                int x1 = (int)MathF.Floor(-halfWidth);
                int x2 = (int)MathF.Ceiling(halfWidth);
                //用一条线段绘制整行
                sb.Draw(pixel, center + new Vector2(x1, y), new Rectangle(0, 0, 1, 1),
                    color, 0f, Vector2.Zero, new Vector2(x2 - x1, 1f), SpriteEffects.None, 0f);
            }
        }

        private static void DrawTargetReticle(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            Color reticleColor = new Color(255, 70, 40) * (alpha * 0.8f);
            float reticleSize = 26f + MathF.Sin(kortoTargetBlinkTimer * 1.5f) * 3f;
            float gap = 8f;
            float lineLen = reticleSize - gap;

            //上
            sb.Draw(pixel, pos + new Vector2(-0.5f, -reticleSize), new Rectangle(0, 0, 1, 1),
                reticleColor, 0f, Vector2.Zero, new Vector2(1f, lineLen), SpriteEffects.None, 0f);
            //下
            sb.Draw(pixel, pos + new Vector2(-0.5f, gap), new Rectangle(0, 0, 1, 1),
                reticleColor, 0f, Vector2.Zero, new Vector2(1f, lineLen), SpriteEffects.None, 0f);
            //左
            sb.Draw(pixel, pos + new Vector2(-reticleSize, -0.5f), new Rectangle(0, 0, 1, 1),
                reticleColor, 0f, Vector2.Zero, new Vector2(lineLen, 1f), SpriteEffects.None, 0f);
            //右
            sb.Draw(pixel, pos + new Vector2(gap, -0.5f), new Rectangle(0, 0, 1, 1),
                reticleColor, 0f, Vector2.Zero, new Vector2(lineLen, 1f), SpriteEffects.None, 0f);
        }

        private static void DrawOrbitLine(SpriteBatch sb, Vector2 from, Vector2 to, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            Color lineColor = new Color(60, 120, 180) * alpha;
            Vector2 dir = to - from;
            float dist = dir.Length();
            if (dist < 1f) return;
            dir /= dist;
            float segAngle = MathF.Atan2(dir.Y, dir.X);

            float dashLen = 4f;
            float gapLen = 6f;
            float pos = 20f; //从恒星附近开始
            while (pos < dist - 5f) {
                Vector2 dashStart = from + dir * pos;
                float len = MathF.Min(dashLen, dist - 5f - pos);
                sb.Draw(pixel, dashStart, new Rectangle(0, 0, 1, 1),
                    lineColor, segAngle, Vector2.Zero,
                    new Vector2(len, 0.5f), SpriteEffects.None, 0f);
                pos += dashLen + gapLen;
            }
        }

        /// <summary>

        /// 行星视图的顶部信息标题

        /// </summary>
        private static void DrawKortoPlanetViewHeader(SpriteBatch sb, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            Color techColor = new Color(60, 160, 220);

            //副标题区域
            float infoY = panelRect.Y + 42f;
            Color infoColor = techColor * (alpha * 0.7f);
            Utils.DrawBorderString(sb, KortoPlanetCountInfo.Value, new Vector2(panelRect.X + 12, infoY),
                infoColor, 0.38f);

            //分隔线
            sb.Draw(pixel, new Vector2(panelRect.X + 6, infoY + 18), new Rectangle(0, 0, 1, 1),
                techColor * (alpha * 0.3f), 0f, Vector2.Zero,
                new Vector2(panelRect.Width - 12, 1f), SpriteEffects.None, 0f);

            //右侧状态信息
            string statusText = KortoStatusCompromised.Value;
            Color statusColor = new Color(255, 80, 40) * (alpha * 0.8f);
            var font = FontAssets.MouseText.Value;
            Vector2 statusSize = font.MeasureString(statusText) * 0.35f;
            Utils.DrawBorderString(sb, statusText,
                new Vector2(panelRect.Right - statusSize.X - 12, infoY),
                statusColor, 0.35f);
        }

        #endregion
    }
}
