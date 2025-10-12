using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    /// <summary>
    /// 深渊复苏进度条UI
    /// </summary>
    internal class ResurrectionUI : UIHandle
    {
        public static ResurrectionUI Instance => UIHandleLoader.GetUIHandleOfType<ResurrectionUI>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None; //手动调用

        private float displayValue = 0f;
        private const float SmoothSpeed = 0.15f;

        private float shakeIntensity = 0f;
        private Vector2 shakeOffset = Vector2.Zero;
        private float pulseTimer = 0f;
        private float glowIntensity = 0f;
        private float warningFlashTimer = 0f;

        private readonly System.Collections.Generic.List<ResurrectionParticle> particles = [];
        private int particleSpawnTimer = 0;

        private readonly System.Collections.Generic.List<ImprovePulse> improvePulses = [];
        private readonly System.Collections.Generic.List<ImproveFlyParticle> improveFlyParticles = [];
        private float improveFlash = 0f;

        private const float DangerThreshold = 0.7f;
        private const float CriticalThreshold = 0.9f;

        public void TriggerImproveEffect(Vector2 worldStartPos, int flyCount) {
            if (flyCount < 1) {
                flyCount = 1;
            }
            if (flyCount > 30) {
                flyCount = 30;
            }
            improveFlash = 1.2f;
            for (int i = 0; i < flyCount; i++) {
                float delay = i * 4f;
                improveFlyParticles.Add(new ImproveFlyParticle(worldStartPos, delay));
            }
        }

        private ResurrectionSystem GetResurrectionSystem() {
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return halibutPlayer.ResurrectionSystem;
            }
            return null;
        }

        public float ResurrectionRatio {
            get {
                var system = GetResurrectionSystem();
                return system?.Ratio ?? 0f;
            }
        }

        private Color GetStateColor(float ratio) {
            if (ratio >= CriticalThreshold) {
                float flash = (float)Math.Sin(warningFlashTimer * 8f) * 0.5f + 0.5f;
                return Color.Lerp(new Color(255, 50, 50), new Color(255, 150, 0), flash);
            }
            else if (ratio >= DangerThreshold) {
                return Color.Lerp(new Color(255, 200, 50), new Color(255, 100, 50), ratio - DangerThreshold);
            }
            else if (ratio >= 0.4f) {
                return Color.Lerp(new Color(100, 200, 255), new Color(255, 200, 50), (ratio - 0.4f) / 0.3f);
            }
            else {
                return Color.Lerp(new Color(50, 150, 255), new Color(100, 200, 255), ratio / 0.4f);
            }
        }

        public override void Update() {
            if (HalibutUIHead.Instance == null || HalibutUIAsset.Resurrection == null) {
                return;
            }

            var resurrectionSystem = GetResurrectionSystem();
            if (resurrectionSystem == null) {
                return;
            }

            Vector2 headPos = HalibutUIHead.Instance.DrawPosition;
            Vector2 headSize = HalibutUIHead.Instance.Size;
            DrawPosition = headPos + new Vector2(headSize.X / 2 + 20, 40);
            Size = HalibutUIAsset.Resurrection.Size();

            float targetValue = resurrectionSystem.CurrentValue;
            displayValue = MathHelper.Lerp(displayValue, targetValue, SmoothSpeed * 0.7f);

            float ratio = resurrectionSystem.Ratio;

            pulseTimer += 0.1f;
            warningFlashTimer += 0.1f;

            if (ratio >= CriticalThreshold) {
                shakeIntensity = MathHelper.Lerp(shakeIntensity, 3f, 0.2f);
                glowIntensity = MathHelper.Lerp(glowIntensity, 1f, 0.2f);
            }
            else if (ratio >= DangerThreshold) {
                shakeIntensity = MathHelper.Lerp(shakeIntensity, 1.5f, 0.15f);
                glowIntensity = MathHelper.Lerp(glowIntensity, 0.6f, 0.15f);
            }
            else {
                shakeIntensity = MathHelper.Lerp(shakeIntensity, 0f, 0.1f);
                glowIntensity = MathHelper.Lerp(glowIntensity, 0.2f, 0.1f);
            }

            if (shakeIntensity > 0.1f) {
                shakeOffset = new Vector2(
                    (float)(Math.Sin(pulseTimer * 20f) * shakeIntensity),
                    (float)(Math.Cos(pulseTimer * 15f) * shakeIntensity * 0.5f)
                );
            }
            else {
                shakeOffset = Vector2.Zero;
            }

            for (int i = particles.Count - 1; i >= 0; i--) {
                particles[i].Update();
                if (particles[i].IsDead) {
                    particles.RemoveAt(i);
                }
            }

            if (ratio > DangerThreshold) {
                particleSpawnTimer++;
                int spawnRate = ratio >= CriticalThreshold ? 3 : 8;
                if (particleSpawnTimer >= spawnRate) {
                    particleSpawnTimer = 0;
                    SpawnParticle(ratio);
                }
            }

            for (int i = improveFlyParticles.Count - 1; i >= 0; i--) {
                improveFlyParticles[i].Update(this);
                if (improveFlyParticles[i].Arrived) {
                    Vector2 center = GetBarCenter();
                    improvePulses.Add(new ImprovePulse(center));
                    improveFlyParticles.RemoveAt(i);
                }
            }

            for (int i = improvePulses.Count - 1; i >= 0; i--) {
                improvePulses[i].Update();
                if (improvePulses[i].Finished) {
                    improvePulses.RemoveAt(i);
                }
            }

            if (improveFlash > 0f) {
                improveFlash -= 0.05f;
                if (improveFlash < 0f) {
                    improveFlash = 0f;
                }
            }

            UIHitBox = DrawPosition.GetRectangle(Size);
        }

        internal Vector2 GetBarCenter() {
            return DrawPosition + shakeOffset + new Vector2(HalibutUIAsset.Resurrection.Width / 2f, HalibutUIAsset.Resurrection.Height / 2f);
        }

        private void SpawnParticle(float ratio) {
            Vector2 barPos = DrawPosition + new Vector2(24, 12) + shakeOffset;
            Vector2 particlePos = barPos + new Vector2(Main.rand.NextFloat(0, 52), Main.rand.NextFloat(-2, 10));
            Vector2 velocity = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, -0.5f));
            Color color = GetStateColor(ratio);
            particles.Add(new ResurrectionParticle(particlePos, velocity, color));
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (HalibutUIHead.Instance == null || !HalibutUIHead.Instance.Active) {
                return;
            }

            var resurrectionSystem = GetResurrectionSystem();
            if (resurrectionSystem == null) {
                return;
            }

            float ratio = displayValue / resurrectionSystem.MaxValue;
            ratio = Math.Clamp(ratio, 0f, 1f);

            Vector2 drawPos = DrawPosition + shakeOffset;

            foreach (var particle in particles) {
                particle.Draw(spriteBatch);
            }
            foreach (var fp in improveFlyParticles) {
                fp.Draw(spriteBatch);
            }

            spriteBatch.Draw(HalibutUIAsset.Resurrection, drawPos + new Vector2(2, 2), null,
                Color.Black * 0.5f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

            spriteBatch.Draw(HalibutUIAsset.Resurrection, drawPos, null,
                Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

            if (ratio > 0.01f) {
                Vector2 barPos = drawPos + new Vector2(24, 12);
                int fillWidth = (int)(52 * ratio);

                if (fillWidth > 0) {
                    Rectangle sourceRect = new Rectangle(0, 0, fillWidth, HalibutUIAsset.ResurrectionTop.Height);
                    Color fillColor = GetStateColor(ratio);

                    spriteBatch.Draw(HalibutUIAsset.ResurrectionTop, barPos + new Vector2(0, 1), sourceRect,
                        fillColor * 0.6f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

                    spriteBatch.Draw(HalibutUIAsset.ResurrectionTop, barPos, sourceRect,
                        fillColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

                    if (glowIntensity > 0.1f) {
                        Color glowColor = fillColor with { A = 0 };
                        float pulse = (float)Math.Sin(pulseTimer * 3f) * 0.3f + 0.7f;

                        spriteBatch.Draw(HalibutUIAsset.ResurrectionTop, barPos, sourceRect,
                            glowColor * glowIntensity * pulse * 0.6f, 0f, Vector2.Zero,
                            new Vector2(1f, 1.2f), SpriteEffects.None, 0f);
                    }

                    Rectangle highlightRect = new Rectangle(0, 0, fillWidth, 2);
                    spriteBatch.Draw(HalibutUIAsset.ResurrectionTop, barPos, highlightRect,
                        Color.White * 0.4f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.Draw(HalibutUIAsset.Resurrection, drawPos, null,
                Color.White * 0.3f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

            if (ratio >= DangerThreshold) {
                float flash = (float)Math.Sin(warningFlashTimer * 6f) * 0.5f + 0.5f;
                Color warningColor = ratio >= CriticalThreshold
                    ? new Color(255, 50, 50)
                    : new Color(255, 200, 50);

                spriteBatch.Draw(HalibutUIAsset.Resurrection, drawPos, null,
                    warningColor with { A = 0 } * flash * 0.5f, 0f, Vector2.Zero,
                    1.05f, SpriteEffects.None, 0f);
            }

            foreach (var pulse in improvePulses) {
                pulse.Draw(spriteBatch);
            }

            if (improveFlash > 0f) {
                float flashAlpha = Math.Min(1f, improveFlash);
                spriteBatch.Draw(HalibutUIAsset.Resurrection, drawPos, null,
                    new Color(120, 220, 255) with { A = 0 } * flashAlpha * 0.5f, 0f, Vector2.Zero, 1.08f, SpriteEffects.None, 0f);
            }

            Rectangle hitBox = new Rectangle((int)drawPos.X, (int)drawPos.Y,
                HalibutUIAsset.Resurrection.Width, HalibutUIAsset.Resurrection.Height);

            if (hitBox.Contains(Main.MouseScreen.ToPoint())) {
                DrawHoverTooltip(spriteBatch, resurrectionSystem, ratio);
            }
        }

        private void DrawHoverTooltip(SpriteBatch spriteBatch, ResurrectionSystem system, float ratio) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float alpha = 0.97f;

            float minWidth = 220f;
            float maxWidth = 420f;
            float horizontalPadding = 14f;
            float topPadding = 10f;
            float bottomPadding = 14f;
            float titleExtra = 6f;
            float dividerSpacing = 8f;
            float infoSpacingTop = 10f;
            float infoLineHeight = 18f;
            float summarySpacing = 6f;
            float summaryLineHeight = 16f;
            float contentRightPadding = 14f;

            float percent = ratio * 100f;
            float cur = system.CurrentValue;
            float max = system.MaxValue;
            float rate = system.ResurrectionRate;

            string rateLevel = GetRateLevel(rate);
            Color rateLevelColor = GetRateLevelColor(rateLevel);
            string stateLine = GetStateSummary(ratio, rate);

            string title = "深渊复苏状态";
            string line1 = $"百分比 : {percent:F1}%";
            string line2 = $"复苏值 : {cur:F1} / {max:F1}";
            string line3 = $"速度   : {rate * 60:F3}/秒";

            float workingWidth = minWidth;
            float contentWidth = workingWidth - horizontalPadding - contentRightPadding;

            float infoMaxLine = 0f;
            infoMaxLine = Math.Max(infoMaxLine, FontAssets.MouseText.Value.MeasureString(line1).X);
            infoMaxLine = Math.Max(infoMaxLine, FontAssets.MouseText.Value.MeasureString(line2).X);
            infoMaxLine = Math.Max(infoMaxLine, FontAssets.MouseText.Value.MeasureString(line3).X + 60f);

            string[] summaryLines = WrapSummary(stateLine, contentWidth);
            float summaryMaxLine = 0f;
            for (int i = 0; i < summaryLines.Length; i++) {
                if (string.IsNullOrWhiteSpace(summaryLines[i])) {
                    continue;
                }
                float w = FontAssets.MouseText.Value.MeasureString(summaryLines[i]).X;
                if (w > summaryMaxLine) {
                    summaryMaxLine = w;
                }
            }

            float longest = Math.Max(infoMaxLine, summaryMaxLine);
            if (longest > contentWidth) {
                workingWidth = Math.Clamp(longest + horizontalPadding + contentRightPadding, minWidth, maxWidth);
                contentWidth = workingWidth - horizontalPadding - contentRightPadding;
                summaryLines = WrapSummary(stateLine, contentWidth);
            }

            int summaryDrawLines = 0;
            for (int i = 0; i < summaryLines.Length; i++) {
                if (!string.IsNullOrWhiteSpace(summaryLines[i])) {
                    summaryDrawLines++;
                }
            }

            float titleHeight = FontAssets.MouseText.Value.MeasureString(title).Y * 0.9f;
            float infoBlockHeight = infoLineHeight * 3f;
            float summaryBlockHeight = summaryDrawLines * summaryLineHeight;
            float dividerHeight = 2f;
            float headerEstimatedHeight = FontAssets.MouseText.Value.MeasureString("当前态势评估").Y * 0.72f + 6f; //预估标题实际占用高度

            float panelHeight = topPadding
                + titleHeight + titleExtra
                + dividerSpacing + dividerHeight
                + infoSpacingTop + infoBlockHeight
                + dividerSpacing + dividerHeight
                + summarySpacing + headerEstimatedHeight + summaryBlockHeight
                + bottomPadding;

            float screenLimit = Main.screenHeight - 40f;
            if (panelHeight > screenLimit) {
                panelHeight = screenLimit;
            }

            Vector2 panelSize = new Vector2(workingWidth, panelHeight);
            Vector2 mousePos = MousePosition + new Vector2(18, -panelSize.Y - 12);
            if (mousePos.X + panelSize.X > Main.screenWidth - 16) {
                mousePos.X = Main.screenWidth - panelSize.X - 16;
            }
            if (mousePos.Y < 16) {
                mousePos.Y = 16;
            }

            Rectangle panelRect = new Rectangle((int)mousePos.X, (int)mousePos.Y, (int)panelSize.X, (int)panelSize.Y);

            Rectangle shadowRect = panelRect;
            shadowRect.Offset(3, 3);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * 0.5f * alpha);
            float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.05f + 0.95f;
            Color baseA = new Color(14, 22, 38) * (alpha * wave);
            Color baseB = new Color(8, 26, 46) * 0.3f;
            Color bgCol = new Color(
                (byte)Math.Clamp(baseA.R + baseB.R, 0, 255),
                (byte)Math.Clamp(baseA.G + baseB.G, 0, 255),
                (byte)Math.Clamp(baseA.B + baseB.B, 0, 255),
                (byte)Math.Clamp(baseA.A + baseB.A, 0, 255)
            );
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), bgCol);

            Color edgeColor = GetStateColor(ratio) * 0.65f * alpha;
            DrawTooltipBorder(spriteBatch, panelRect, edgeColor);

            Vector2 titlePos = new Vector2(panelRect.X + horizontalPadding, panelRect.Y + topPadding);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f;
                Vector2 o = ang.ToRotationVector2() * 1.25f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + o, edgeColor * 0.55f, 0.9f);
            }
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * alpha, 0.9f);

            Vector2 dividerStart = titlePos + new Vector2(0, titleHeight + titleExtra);
            Vector2 dividerEnd = dividerStart + new Vector2(panelSize.X - horizontalPadding - contentRightPadding, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd, edgeColor * 0.9f, edgeColor * 0.05f, 1.3f);

            Vector2 infoStart = dividerStart + new Vector2(0, dividerSpacing + infoSpacingTop);
            int infoIndex = 0;
            DrawInfoLine(spriteBatch, line1, infoStart, ref infoIndex, infoLineHeight, alpha, Color.White);
            DrawInfoLine(spriteBatch, line2, infoStart, ref infoIndex, infoLineHeight, alpha, Color.White);
            DrawInfoLineRate(spriteBatch, line3, rateLevel, rateLevelColor, infoStart, ref infoIndex, infoLineHeight, alpha);

            Vector2 divider2Start = infoStart + new Vector2(0, infoIndex * infoLineHeight + dividerSpacing * 0.6f);
            Vector2 divider2End = divider2Start + new Vector2(panelSize.X - horizontalPadding - contentRightPadding, 0);
            DrawDashedLine(spriteBatch, divider2Start, divider2End, edgeColor * 0.6f, 6f, 3f);

            //绘制摘要标题并计算真实高度
            Vector2 summaryHeaderPos = divider2Start + new Vector2(0, dividerSpacing + summarySpacing);
            float headerHeight = DrawSummaryHeader(spriteBatch, summaryHeaderPos, edgeColor, alpha);
            Vector2 summaryStart = summaryHeaderPos + new Vector2(0, headerHeight + 4f);
            DrawSummaryLines(spriteBatch, summaryLines, summaryStart, panelRect, summaryLineHeight, alpha);

            float starTime = Main.GlobalTimeWrappedHourly * 3f;
            Vector2 star1 = new Vector2(panelRect.Right - 18, panelRect.Y + 14);
            float s1a = ((float)Math.Sin(starTime) * 0.5f + 0.5f) * alpha;
            DrawStar(spriteBatch, star1, 4f, edgeColor * s1a);
            Vector2 star2 = new Vector2(panelRect.Right - 34, panelRect.Bottom - 20);
            float s2a = ((float)Math.Sin(starTime + MathHelper.Pi) * 0.5f + 0.5f) * alpha;
            DrawStar(spriteBatch, star2, 3f, edgeColor * s2a);
        }

        private static string[] WrapSummary(string text, float contentWidth) {
            return Utils.WordwrapString(text, FontAssets.MouseText.Value, (int)(contentWidth + 40), 20, out int _);
        }

        private static void DrawInfoLine(SpriteBatch sb, string text, Vector2 start, ref int index, float lineHeight, float alpha, Color baseColor) {
            Vector2 pos = start + new Vector2(0, index * lineHeight);
            Utils.DrawBorderString(sb, text, pos + new Vector2(1, 1), Color.Black * 0.55f * alpha, 0.78f);
            Utils.DrawBorderString(sb, text, pos, baseColor * alpha, 0.78f);
            index++;
        }

        private static void DrawInfoLineRate(SpriteBatch sb, string textPrefix, string level, Color levelColor, Vector2 start, ref int index, float lineHeight, float alpha) {
            Vector2 pos = start + new Vector2(0, index * lineHeight);
            string composed = textPrefix + "  [" + level + "]";
            Utils.DrawBorderString(sb, composed, pos + new Vector2(1, 1), Color.Black * 0.6f * alpha, 0.78f);
            Utils.DrawBorderString(sb, textPrefix, pos, Color.White * alpha, 0.78f);
            Vector2 prefixSize = FontAssets.MouseText.Value.MeasureString(textPrefix);
            Vector2 levelPos = pos + new Vector2(prefixSize.X - 16, 0);
            Utils.DrawBorderString(sb, "[" + level + "]", levelPos, levelColor * alpha, 0.78f);
            index++;
        }

        private static float DrawSummaryHeader(SpriteBatch sb, Vector2 pos, Color edgeColor, float alpha) {
            string header = "当前态势评估";
            for (int i = 0; i < 4; i++) {
                float a = MathHelper.TwoPi * i / 4f;
                Vector2 o = a.ToRotationVector2() * 1.1f;
                Utils.DrawBorderString(sb, header, pos + o, edgeColor * 0.4f * alpha, 0.72f);
            }
            Utils.DrawBorderString(sb, header, pos, Color.Lerp(edgeColor, Color.White, 0.4f) * alpha, 0.72f);
            Vector2 size = FontAssets.MouseText.Value.MeasureString(header) * 0.72f;
            return size.Y; //返回高度供布局使用
        }

        private static void DrawSummaryLines(SpriteBatch sb, string[] lines, Vector2 start, Rectangle panelRect, float lineHeight, float alpha) {
            int drawn = 0;
            for (int i = 0; i < lines.Length; i++) {
                if (string.IsNullOrWhiteSpace(lines[i])) {
                    continue;
                }
                string line = lines[i].TrimEnd('-', ' ');
                Vector2 pos = start + new Vector2(2, drawn * lineHeight);
                if (pos.Y + (lineHeight - 2f) > panelRect.Bottom - 10) {
                    break;
                }
                Utils.DrawBorderString(sb, line, pos + new Vector2(1, 1), Color.Black * alpha * 0.5f, 0.7f);
                Utils.DrawBorderString(sb, line, pos, new Color(230, 240, 255) * alpha, 0.7f);
                drawn++;
            }
        }

        private static Color GetRateLevelColor(string level) {
            if (level == "极低") {
                return new Color(120, 200, 255);
            }
            else if (level == "低") {
                return new Color(100, 220, 170);
            }
            else if (level == "中") {
                return new Color(255, 210, 90);
            }
            else if (level == "高") {
                return new Color(255, 140, 70);
            }
            else {
                return new Color(255, 70, 70);
            }
        }

        private static string GetRateLevel(float rate) {
            if (rate < 0.01f) {
                return "极低";
            }
            else if (rate < 0.025f) {
                return "低";
            }
            else if (rate < 0.05f) {
                return "中";
            }
            else if (rate < 0.09f) {
                return "高";
            }
            else {
                return "危险";
            }
        }

        private static string GetStateSummary(float ratio, float rate) {
            string phase;
            if (ratio < 0.25f) {
                phase = "复苏平稳，尚无明显异象";
            }
            else if (ratio < 0.5f) {
                phase = "局势渐起波纹，能量仍可控";
            }
            else if (ratio < 0.7f) {
                phase = "脉冲已具侵蚀感，需要留意";
            }
            else if (ratio < 0.9f) {
                phase = "高压区形成，领域边缘不稳定";
            }
            else {
                phase = "深渊临界——随时可能失控";
            }

            string trend;
            if (rate < 0.01f) {
                trend = "几乎静止";
            }
            else if (rate < 0.025f) {
                trend = "缓慢上升";
            }
            else if (rate < 0.05f) {
                trend = "稳态攀升";
            }
            else if (rate < 0.09f) {
                trend = "快速累积";
            }
            else {
                trend = "危险激增";
            }

            return $"状态：{phase}。当前增长趋势：{trend}。请根据态势调整领域或研究策略";
        }

        private void DrawTooltipBorder(SpriteBatch sb, Rectangle rect, Color glow) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle top = new Rectangle(rect.X, rect.Y, rect.Width, 1);
            Rectangle bottom = new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1);
            Rectangle left = new Rectangle(rect.X, rect.Y, 1, rect.Height);
            Rectangle right = new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height);
            sb.Draw(pixel, top, new Rectangle(0, 0, 1, 1), glow);
            sb.Draw(pixel, bottom, new Rectangle(0, 0, 1, 1), glow * 0.7f);
            sb.Draw(pixel, left, new Rectangle(0, 0, 1, 1), glow * 0.85f);
            sb.Draw(pixel, right, new Rectangle(0, 0, 1, 1), glow * 0.85f);
            DrawCorner(sb, new Vector2(rect.Left, rect.Top), glow * 1.1f, 0f);
            DrawCorner(sb, new Vector2(rect.Right, rect.Top), glow * 1.1f, MathHelper.PiOver2);
            DrawCorner(sb, new Vector2(rect.Right, rect.Bottom), glow * 1.1f, MathHelper.Pi);
            DrawCorner(sb, new Vector2(rect.Left, rect.Bottom), glow * 1.1f, MathHelper.Pi + MathHelper.PiOver2);
        }

        private void DrawCorner(SpriteBatch sb, Vector2 pos, Color color, float rot) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            for (int i = 0; i < 3; i++) {
                float len = 6 - i * 2;
                sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * (0.9f - i * 0.3f), rot, new Vector2(0, 0.5f), new Vector2(len, 1f), SpriteEffects.None, 0f);
            }
        }

        private void DrawGradientLine(SpriteBatch sb, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }
            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 10f));
            for (int i = 0; i < segments; i++) {
                float t = (float)i / segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                sb.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }

        private void DrawDashedLine(SpriteBatch sb, Vector2 start, Vector2 end, Color color, float dashLength, float gapLength) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 dir = end - start;
            float len = dir.Length();
            if (len < 1f) {
                return;
            }
            dir.Normalize();
            float drawn = 0f;
            while (drawn < len) {
                float seg = Math.Min(dashLength, len - drawn);
                Vector2 segStart = start + dir * drawn;
                sb.Draw(pixel, segStart, new Rectangle(0, 0, 1, 1), color, dir.ToRotation(), new Vector2(0, 0.5f), new Vector2(seg, 1.2f), SpriteEffects.None, 0f);
                drawn += dashLength + gapLength;
            }
        }

        private void DrawStar(SpriteBatch sb, Vector2 position, float size, Color color) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            sb.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0);
            sb.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0);
            sb.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color * 0.7f, MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.7f, size * 0.2f), SpriteEffects.None, 0);
            sb.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color * 0.7f, -MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.7f, size * 0.2f), SpriteEffects.None, 0);
        }
    }

    internal class ResurrectionParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Color Color;
        public float Scale;
        public float Alpha;
        public int Life;
        public int MaxLife;
        public bool IsDead => Life >= MaxLife;
        public ResurrectionParticle(Vector2 position, Vector2 velocity, Color color) {
            Position = position;
            Velocity = velocity;
            Color = color;
            Scale = Main.rand.NextFloat(0.5f, 1.2f);
            Alpha = 1f;
            Life = 0;
            MaxLife = Main.rand.Next(30, 60);
        }
        public void Update() {
            Life++;
            Position += Velocity;
            Velocity.Y += 0.1f;
            Velocity *= 0.98f;
            float lifeRatio = Life / (float)MaxLife;
            Alpha = 1f - lifeRatio;
            Scale *= 0.98f;
        }
        public void Draw(SpriteBatch spriteBatch) {
            if (IsDead) {
                return;
            }
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Color drawColor = Color * Alpha;
            spriteBatch.Draw(pixel, Position, new Rectangle(0, 0, 1, 1),
                drawColor, 0f, new Vector2(0.5f, 0.5f),
                Scale * 2f, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, Position, new Rectangle(0, 0, 1, 1),
                drawColor with { A = 0 } * 0.5f, 0f, new Vector2(0.5f, 0.5f),
                Scale * 4f, SpriteEffects.None, 0f);
        }
    }

    internal class ImproveFlyParticle
    {
        private Vector2 startPos;
        private Vector2 currentPos;
        private float time;
        private float delay;
        private float progress;
        private float duration;
        private float rotation;
        private float scale;
        public bool Arrived;
        public ImproveFlyParticle(Vector2 start, float delayFrames) {
            startPos = start;
            currentPos = start;
            delay = delayFrames;
            duration = 50f + Main.rand.NextFloat(10f);
            rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            scale = Main.rand.NextFloat(0.6f, 1.1f);
            Arrived = false;
        }
        public void Update(ResurrectionUI ui) {
            if (Arrived) {
                return;
            }
            time++;
            if (time < delay) {
                return;
            }
            float t = (time - delay) / duration;
            if (t >= 1f) {
                t = 1f;
                Arrived = true;
            }
            progress = EaseOutCubic(t);
            Vector2 target = ui.GetBarCenter();
            Vector2 mid = (startPos + target) / 2f + new Vector2(0, -40f - Main.rand.NextFloat(30f));
            currentPos = Bezier(startPos, mid, target, progress);
            rotation += 0.15f;
        }
        public void Draw(SpriteBatch spriteBatch) {
            if (Arrived) {
                return;
            }
            if (time < delay) {
                return;
            }
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float alpha = 1f - progress * 0.3f;
            Color col = Color.Lerp(new Color(80, 180, 255), new Color(200, 240, 255), progress) * alpha;
            Vector2 size = new Vector2(6f * scale, 2f * scale);
            spriteBatch.Draw(pixel, currentPos, new Rectangle(0, 0, 1, 1), col, rotation, new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, currentPos, new Rectangle(0, 0, 1, 1), col * 0.5f, rotation + MathHelper.PiOver2, new Vector2(0.5f, 0.5f), size * 0.6f, SpriteEffects.None, 0f);
        }
        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t) {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }
        private static float EaseOutCubic(float t) {
            return 1f - (float)Math.Pow(1f - t, 3f);
        }
    }

    internal class ImprovePulse
    {
        private Vector2 center;
        private float progress;
        private const float Duration = 40f;
        public bool Finished => progress >= 1f;
        public ImprovePulse(Vector2 c) {
            center = c;
            progress = 0f;
        }
        public void Update() {
            if (Finished) {
                return;
            }
            progress += 1f / Duration;
            if (progress > 1f) {
                progress = 1f;
            }
        }
        public void Draw(SpriteBatch spriteBatch) {
            if (Finished) {
                return;
            }
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float eased = EaseOutCubic(progress);
            float radius = MathHelper.Lerp(10f, 95f, eased);
            float thickness = MathHelper.Lerp(8f, 1.5f, eased);
            float alpha = 1f - eased;
            Color col = Color.Lerp(new Color(90, 190, 255), new Color(200, 230, 255), eased) * alpha * 0.8f;
            int segs = 60;
            float step = MathHelper.TwoPi / segs;
            for (int i = 0; i < segs; i++) {
                float a1 = i * step;
                float a2 = (i + 1) * step;
                Vector2 p1 = center + a1.ToRotationVector2() * radius;
                Vector2 p2 = center + a2.ToRotationVector2() * radius;
                Vector2 diff = p2 - p1;
                float len = diff.Length();
                float rot = diff.ToRotation();
                spriteBatch.Draw(pixel, p1, new Rectangle(0, 0, 1, 1), col, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
            }
        }
        private static float EaseOutCubic(float t) {
            return 1f - (float)Math.Pow(1f - t, 3f);
        }
    }
}
