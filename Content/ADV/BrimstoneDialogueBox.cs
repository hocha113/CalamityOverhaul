using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.ADV
{
    /// <summary>
    /// 至尊灾厄硫磺火风格对话框
    /// </summary>
    internal class BrimstoneDialogueBox : DialogueBoxBase
    {
        public static BrimstoneDialogueBox Instance => UIHandleLoader.GetUIHandleOfType<BrimstoneDialogueBox>();
        public override string LocalizationCategory => "Legend.HalibutText";

        //风格参数
        private const float FixedWidth = 540f;
        protected override float PanelWidth => FixedWidth;

        //火焰动画参数
        private float flameTimer = 0f;
        private float emberGlowTimer = 0f;
        private float heatWavePhase = 0f;
        private float infernoPulse = 0f;

        //粒子系统
        private readonly List<EmberParticle> embers = new();
        private int emberSpawnTimer = 0;
        private readonly List<AshParticle> ashes = new();
        private int ashSpawnTimer = 0;
        private readonly List<FlameWisp> flameWisps = new();
        private int wispSpawnTimer = 0;
        private const float ParticleSideMargin = 30f;

        protected override void StyleUpdate(Vector2 panelPos, Vector2 panelSize) {
            //火焰动画计时器
            flameTimer += 0.045f;
            emberGlowTimer += 0.038f;
            heatWavePhase += 0.025f;
            infernoPulse += 0.012f;
            if (flameTimer > MathHelper.TwoPi) flameTimer -= MathHelper.TwoPi;
            if (emberGlowTimer > MathHelper.TwoPi) emberGlowTimer -= MathHelper.TwoPi;
            if (heatWavePhase > MathHelper.TwoPi) heatWavePhase -= MathHelper.TwoPi;
            if (infernoPulse > MathHelper.TwoPi) infernoPulse -= MathHelper.TwoPi;

            //余烬粒子生成
            emberSpawnTimer++;
            if (Active && emberSpawnTimer >= 8 && embers.Count < 35) {
                emberSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(panelPos.X + ParticleSideMargin, panelPos.X + panelSize.X - ParticleSideMargin);
                Vector2 startPos = new(xPos, panelPos.Y + panelSize.Y - 5f);
                embers.Add(new EmberParticle(startPos));
            }
            for (int i = embers.Count - 1; i >= 0; i--) {
                if (embers[i].Update(panelPos, panelSize)) {
                    embers.RemoveAt(i);
                }
            }

            //灰烬粒子生成
            ashSpawnTimer++;
            if (Active && ashSpawnTimer >= 12 && ashes.Count < 25) {
                ashSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(panelPos.X + ParticleSideMargin, panelPos.X + panelSize.X - ParticleSideMargin);
                Vector2 startPos = new(xPos, panelPos.Y + panelSize.Y);
                ashes.Add(new AshParticle(startPos));
            }
            for (int i = ashes.Count - 1; i >= 0; i--) {
                if (ashes[i].Update(panelPos, panelSize)) {
                    ashes.RemoveAt(i);
                }
            }

            //火焰精灵生成
            wispSpawnTimer++;
            if (Active && wispSpawnTimer >= 45 && flameWisps.Count < 8) {
                wispSpawnTimer = 0;
                Vector2 startPos = new(
                    Main.rand.NextFloat(panelPos.X + 40f, panelPos.X + panelSize.X - 40f),
                    Main.rand.NextFloat(panelPos.Y + 60f, panelPos.Y + panelSize.Y - 60f)
                );
                flameWisps.Add(new FlameWisp(startPos));
            }
            for (int i = flameWisps.Count - 1; i >= 0; i--) {
                if (flameWisps[i].Update(panelPos, panelSize)) {
                    flameWisps.RemoveAt(i);
                }
            }
        }

        protected override void DrawStyle(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha, float easedProgress) {
            Texture2D vaule = VaultAsset.placeholder2.Value;

            //阴影效果
            Rectangle shadow = panelRect;
            shadow.Offset(7, 9);
            spriteBatch.Draw(vaule, shadow, new Rectangle(0, 0, 1, 1), new Color(20, 0, 0) * (alpha * 0.65f));

            //渐变背景 - 硫磺火深红色
            int segments = 35;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                //硫磺火色调：深红到暗橙
                Color brimstoneDeep = new Color(25, 5, 5);      //深暗红
                Color brimstoneMid = new Color(80, 15, 10);     //中等暗红
                Color brimstoneHot = new Color(140, 35, 20);    //热红

                float breathing = (float)Math.Sin(infernoPulse * 1.5f) * 0.5f + 0.5f;
                float flameWave = (float)Math.Sin(flameTimer * 0.6f + t * 2.2f) * 0.5f + 0.5f;

                Color baseColor = Color.Lerp(brimstoneDeep, brimstoneMid, flameWave);
                Color finalColor = Color.Lerp(baseColor, brimstoneHot, t * 0.5f * (0.3f + breathing * 0.7f));
                finalColor *= alpha * 0.92f;

                spriteBatch.Draw(vaule, r, new Rectangle(0, 0, 1, 1), finalColor);
            }

            //火焰脉冲叠加层
            float pulseBrightness = (float)Math.Sin(infernoPulse * 1.8f) * 0.5f + 0.5f;
            Color pulseOverlay = new Color(120, 25, 15) * (alpha * 0.25f * pulseBrightness);
            spriteBatch.Draw(vaule, panelRect, new Rectangle(0, 0, 1, 1), pulseOverlay);

            //热浪扭曲效果层
            DrawHeatWaveOverlay(spriteBatch, panelRect, alpha * 0.85f);

            //内发光
            float glowPulse = (float)Math.Sin(emberGlowTimer * 1.5f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-7, -7);
            spriteBatch.Draw(vaule, inner, new Rectangle(0, 0, 1, 1), new Color(180, 60, 30) * (alpha * 0.12f * (0.5f + glowPulse * 0.5f)));

            //绘制火焰边框
            DrawBrimstoneFrame(spriteBatch, panelRect, alpha, glowPulse);

            //绘制粒子（从后到前）
            foreach (var ash in ashes) {
                ash.Draw(spriteBatch, alpha * 0.7f);
            }
            foreach (var wisp in flameWisps) {
                wisp.Draw(spriteBatch, alpha * 0.8f);
            }
            foreach (var ember in embers) {
                ember.Draw(spriteBatch, alpha * 0.95f);
            }

            if (current == null || contentAlpha <= 0.01f) {
                return;
            }

            DrawPortraitAndText(spriteBatch, panelRect, alpha, contentAlpha);
        }

        private void DrawPortraitAndText(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            bool hasPortrait = false;
            PortraitData speakerPortrait = null;
            if (current != null && !string.IsNullOrEmpty(current.Speaker) && portraits.TryGetValue(current.Speaker, out var pd) && pd.Texture != null && pd.Fade > 0.02f) {
                hasPortrait = true;
                speakerPortrait = pd;
            }

            float switchEase = speakerSwitchProgress;
            if (switchEase < 1f) {
                switchEase = CWRUtils.EaseOutCubic(switchEase);
            }
            float portraitAppearScale = MathHelper.Lerp(0.82f, 1f, switchEase);
            float portraitExtraAlpha = MathHelper.Clamp(switchEase, 0f, 1f);

            float leftOffset = Padding;
            float topNameOffset = 12f;
            float textBlockOffsetY = Padding + 38;

            if (hasPortrait) {
                float availHeight = panelRect.Height - 60f;
                float maxPortraitHeight = Math.Clamp(availHeight, 95f, 270f);
                Texture2D ptex = speakerPortrait.Texture;
                float scaleBase = Math.Min(PortraitWidth / ptex.Width, maxPortraitHeight / ptex.Height);
                float scale = scaleBase * portraitAppearScale;
                Vector2 pSize = ptex.Size() * scale;
                Vector2 pPos = new(panelRect.X + Padding + PortraitInnerPadding, panelRect.Y + panelRect.Height - pSize.Y - Padding - 12f);

                DrawBrimstonePortraitFrame(spriteBatch, new Rectangle((int)(pPos.X - 9), (int)(pPos.Y - 9), (int)(pSize.X + 18), (int)(pSize.Y + 18))
                    , alpha * speakerPortrait.Fade * portraitExtraAlpha);

                Color drawColor = speakerPortrait.BaseColor * contentAlpha * speakerPortrait.Fade * portraitExtraAlpha;
                if (speakerPortrait.Silhouette) {
                    drawColor = new Color(40, 10, 5) * (contentAlpha * speakerPortrait.Fade * portraitExtraAlpha) * 0.85f;
                }

                spriteBatch.Draw(ptex, pPos, null, drawColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                //火焰边缘光效
                float flamePulse = (float)Math.Sin(flameTimer * 1.8f + speakerPortrait.Fade) * 0.5f + 0.5f;
                Color flameRim = new Color(255, 120, 60) * (contentAlpha * 0.5f * flamePulse * speakerPortrait.Fade) * portraitExtraAlpha;
                DrawFlameGlow(spriteBatch, new Rectangle((int)pPos.X - 5, (int)pPos.Y - 5, (int)pSize.X + 10, (int)pSize.Y + 10), flameRim);

                leftOffset += PortraitWidth + 22f;
            }

            //绘制说话者名字
            if (current != null && !string.IsNullOrEmpty(current.Speaker)) {
                Vector2 speakerPos = new(panelRect.X + leftOffset, panelRect.Y + topNameOffset - (1f - switchEase) * 7f);
                float nameAlpha = contentAlpha * switchEase;

                //火焰光晕效果
                Color nameGlow = new Color(255, 140, 80) * nameAlpha * 0.75f;
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi * i / 6f + flameTimer * 0.5f;
                    Vector2 offset = angle.ToRotationVector2() * 2.2f * switchEase;
                    Utils.DrawBorderString(spriteBatch, current.Speaker, speakerPos + offset, nameGlow * 0.5f, 0.95f);
                }

                Utils.DrawBorderString(spriteBatch, current.Speaker, speakerPos, new Color(255, 240, 220) * nameAlpha, 0.95f);

                //分隔线
                Vector2 divStart = speakerPos + new Vector2(0, 28);
                Vector2 divEnd = divStart + new Vector2(panelRect.Width - leftOffset - Padding, 0);
                float lineAlpha = contentAlpha * switchEase;
                DrawFlameGradientLine(spriteBatch, divStart, divEnd,
                    new Color(220, 80, 40) * (lineAlpha * 0.9f),
                    new Color(120, 30, 15) * (lineAlpha * 0.1f), 1.5f);
            }

            //绘制文本
            Vector2 textStart = new(panelRect.X + leftOffset, panelRect.Y + textBlockOffsetY);
            int remaining = visibleCharCount;
            int lineHeight = (int)(font.MeasureString("A").Y * 0.8f) + LineSpacing;
            int maxLines = (int)((panelRect.Height - (textStart.Y - panelRect.Y) - Padding) / lineHeight);

            for (int i = 0; i < wrappedLines.Length && i < maxLines; i++) {
                string fullLine = wrappedLines[i];
                if (string.IsNullOrEmpty(fullLine)) {
                    continue;
                }

                string visLine;
                if (finishedCurrent) {
                    visLine = fullLine;
                }
                else {
                    if (remaining <= 0) {
                        break;
                    }
                    int take = Math.Min(fullLine.Length, remaining);
                    visLine = fullLine[..take];
                    remaining -= take;
                }

                Vector2 linePos = textStart + new Vector2(0, i * lineHeight);
                if (linePos.Y + lineHeight > panelRect.Bottom - Padding) {
                    break;
                }

                //热浪扭曲效果
                float heatWobble = (float)Math.Sin(heatWavePhase * 3f + i * 0.65f) * 0.9f;
                Vector2 wobblePos = linePos + new Vector2(heatWobble, 0);

                //火焰色文字
                Color lineColor = Color.Lerp(new Color(255, 220, 200), new Color(255, 240, 220), 0.4f) * contentAlpha;

                //添加微弱的火焰光晕
                Color textGlow = new Color(255, 150, 80) * (contentAlpha * 0.15f);
                Utils.DrawBorderString(spriteBatch, visLine, wobblePos + new Vector2(0, 1), textGlow, 0.8f);
                Utils.DrawBorderString(spriteBatch, visLine, wobblePos, lineColor, 0.8f);
            }

            //继续提示
            if (waitingForAdvance) {
                float blink = (float)Math.Sin(advanceBlinkTimer / 10f * MathHelper.TwoPi) * 0.5f + 0.5f;
                string hint = $"▶ {ContinueHint.Value} ◀";
                Vector2 hintSize = font.MeasureString(hint) * 0.65f;
                Vector2 hintPos = new(panelRect.Right - Padding - hintSize.X, panelRect.Bottom - Padding - hintSize.Y);

                Color hintGlow = new Color(255, 160, 90) * blink * contentAlpha;
                Utils.DrawBorderString(spriteBatch, hint, hintPos, hintGlow, 0.85f);
            }

            //加速提示
            if (!finishedCurrent) {
                string fast = FastHint.Value;
                Vector2 fastSize = font.MeasureString(fast) * 0.6f;
                Vector2 fastPos = new(panelRect.Right - Padding - fastSize.X, panelRect.Bottom - Padding - fastSize.Y - 18);
                Utils.DrawBorderString(spriteBatch, fast, fastPos, new Color(200, 140, 100) * 0.45f * contentAlpha, 0.7f);
            }
        }

        #region 样式工具函数
        private void DrawHeatWaveOverlay(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            int waveCount = 8;
            for (int i = 0; i < waveCount; i++) {
                float t = i / (float)waveCount;
                float baseY = rect.Y + 25 + t * (rect.Height - 50);
                float amplitude = 5f + (float)Math.Sin((heatWavePhase + t * 1.2f) * 2.5f) * 3.5f;
                float thickness = 1.8f;

                int segments = 50;
                Vector2 prevPoint = Vector2.Zero;
                for (int s = 0; s <= segments; s++) {
                    float progress = s / (float)segments;
                    float waveY = baseY + (float)Math.Sin(heatWavePhase * 3f + progress * MathHelper.TwoPi * 1.5f + t * 2f) * amplitude;
                    Vector2 point = new(rect.X + 12 + progress * (rect.Width - 24), waveY);

                    if (s > 0) {
                        Vector2 diff = point - prevPoint;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            Color waveColor = new Color(180, 60, 30) * (alpha * 0.08f);
                            sb.Draw(vaule, prevPoint, new Rectangle(0, 0, 1, 1), waveColor, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prevPoint = point;
                }
            }
        }

        private static void DrawBrimstoneFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D vaule = VaultAsset.placeholder2.Value;

            //外框 - 火焰橙红
            Color outerEdge = Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), pulse) * (alpha * 0.85f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            sb.Draw(vaule, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            //内框发光
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(220, 100, 50) * (alpha * 0.22f * pulse);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            sb.Draw(vaule, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);

            //角落火焰标记
            DrawFlameMark(sb, new Vector2(rect.X + 12, rect.Y + 12), alpha * 0.95f);
            DrawFlameMark(sb, new Vector2(rect.Right - 12, rect.Y + 12), alpha * 0.95f);
            DrawFlameMark(sb, new Vector2(rect.X + 12, rect.Bottom - 12), alpha * 0.65f);
            DrawFlameMark(sb, new Vector2(rect.Right - 12, rect.Bottom - 12), alpha * 0.65f);
        }

        private static void DrawFlameMark(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            float size = 6f;
            Color flameColor = new Color(255, 150, 70) * alpha;

            //交叉火焰标记
            sb.Draw(vaule, pos, new Rectangle(0, 0, 1, 1), flameColor, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 1.2f, size * 0.3f), SpriteEffects.None, 0f);
            sb.Draw(vaule, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.85f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size * 1.2f, size * 0.3f), SpriteEffects.None, 0f);
            sb.Draw(vaule, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.7f, MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.9f, size * 0.25f), SpriteEffects.None, 0f);
            sb.Draw(vaule, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.7f, -MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.9f, size * 0.25f), SpriteEffects.None, 0f);
        }

        private static void DrawFlameGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }
            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 10f));
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(vaule, segPos, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }
        #endregion

        #region 粒子内部类
        private class EmberParticle(Vector2 start)
        {
            public Vector2 Pos = start;
            public float Size = Main.rand.NextFloat(2.5f, 5.5f);
            public float RiseSpeed = Main.rand.NextFloat(0.4f, 1.1f);
            public float Drift = Main.rand.NextFloat(-0.25f, 0.25f);
            public float Life = 0f;
            public float MaxLife = Main.rand.NextFloat(70f, 130f);
            public float Seed = Main.rand.NextFloat(10f);
            public float RotationSpeed = Main.rand.NextFloat(-0.05f, 0.05f);
            public float Rotation = Main.rand.NextFloat(MathHelper.TwoPi);

            public bool Update(Vector2 panelPos, Vector2 panelSize) {
                Life++;
                float t = Life / MaxLife;
                Pos.Y -= RiseSpeed * (1f - t * 0.3f);
                Pos.X += (float)Math.Sin(Life * 0.06f + Seed) * Drift;
                Rotation += RotationSpeed;

                if (Life >= MaxLife || Pos.Y < panelPos.Y + 15f) {
                    return true;
                }
                return false;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D vaule = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI);
                float scale = Size * (1f + (float)Math.Sin((Life + Seed * 20f) * 0.12f) * 0.15f);

                //火焰余烬颜色：橙红到深红
                Color emberCore = Color.Lerp(new Color(255, 180, 80), new Color(255, 80, 40), t) * (alpha * 0.85f * fade);
                Color emberGlow = Color.Lerp(new Color(255, 140, 60), new Color(180, 40, 20), t) * (alpha * 0.5f * fade);

                //光晕
                sb.Draw(vaule, Pos, new Rectangle(0, 0, 1, 1), emberGlow, 0f, new Vector2(0.5f, 0.5f), scale * 2.2f, SpriteEffects.None, 0f);
                //核心
                sb.Draw(vaule, Pos, new Rectangle(0, 0, 1, 1), emberCore, Rotation, new Vector2(0.5f, 0.5f), scale, SpriteEffects.None, 0f);
            }
        }

        private class AshParticle(Vector2 start)
        {
            public Vector2 Pos = start;
            public float Size = Main.rand.NextFloat(1.5f, 3.5f);
            public float RiseSpeed = Main.rand.NextFloat(0.15f, 0.45f);
            public float Drift = Main.rand.NextFloat(-0.35f, 0.35f);
            public float Life = 0f;
            public float MaxLife = Main.rand.NextFloat(100f, 180f);
            public float Seed = Main.rand.NextFloat(10f);
            public float Rotation = Main.rand.NextFloat(MathHelper.TwoPi);

            public bool Update(Vector2 panelPos, Vector2 panelSize) {
                Life++;
                float t = Life / MaxLife;
                Pos.Y -= RiseSpeed * (0.7f + (float)Math.Sin(t * Math.PI) * 0.3f);
                Pos.X += (float)Math.Sin(Life * 0.04f + Seed) * Drift * 1.5f;

                if (Life >= MaxLife || Pos.Y < panelPos.Y) {
                    return true;
                }
                return false;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D px = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI) * (1f - t * 0.4f);

                //灰烬颜色：深灰到黑
                Color ashColor = Color.Lerp(new Color(60, 50, 45), new Color(30, 20, 15), t) * (alpha * 0.65f * fade);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), ashColor, Rotation, new Vector2(0.5f, 0.5f), Size, SpriteEffects.None, 0f);
            }
        }

        private class FlameWisp(Vector2 start)
        {
            public Vector2 Pos = start;
            public Vector2 Velocity = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(0.3f, 0.8f);
            public float Size = Main.rand.NextFloat(8f, 16f);
            public float Life = 0f;
            public float MaxLife = Main.rand.NextFloat(120f, 200f);
            public float Seed = Main.rand.NextFloat(10f);
            public float Phase = Main.rand.NextFloat(MathHelper.TwoPi);

            public bool Update(Vector2 panelPos, Vector2 panelSize) {
                Life++;
                float t = Life / MaxLife;

                //漂浮运动
                Phase += 0.08f;
                Vector2 drift = new Vector2(
                    (float)Math.Sin(Phase + Seed) * 0.5f,
                    (float)Math.Cos(Phase * 1.3f + Seed * 1.5f) * 0.3f
                );
                Pos += Velocity + drift;

                //边界检查
                if (Pos.X < panelPos.X + 20f || Pos.X > panelPos.X + panelSize.X - 20f) {
                    Velocity.X *= -0.8f;
                }
                if (Pos.Y < panelPos.Y + 40f || Pos.Y > panelPos.Y + panelSize.Y - 40f) {
                    Velocity.Y *= -0.8f;
                }

                if (Life >= MaxLife) {
                    return true;
                }
                return false;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D vaule = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI);
                float pulse = (float)Math.Sin(Life * 0.15f + Seed) * 0.5f + 0.5f;

                float scale = Size * (0.8f + pulse * 0.4f);

                //火焰精灵颜色
                Color wispCore = new Color(255, 200, 120) * (alpha * 0.6f * fade);
                Color wispGlow = new Color(255, 120, 60) * (alpha * 0.3f * fade);

                //外层光晕
                sb.Draw(vaule, Pos, new Rectangle(0, 0, 1, 1), wispGlow, 0f, new Vector2(0.5f, 0.5f), scale * 3f, SpriteEffects.None, 0f);
                //内层核心
                sb.Draw(vaule, Pos, new Rectangle(0, 0, 1, 1), wispCore, 0f, new Vector2(0.5f, 0.5f), scale * 1.2f, SpriteEffects.None, 0f);
            }
        }
        #endregion

        #region 公共静态绘制函数
        private static void DrawBrimstonePortraitFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D vaule = VaultAsset.placeholder2.Value;

            //深色背景
            Color back = new Color(20, 5, 5) * (alpha * 0.88f);
            sb.Draw(vaule, rect, new Rectangle(0, 0, 1, 1), back);

            //火焰边框
            Color edge = new Color(200, 80, 40) * (alpha * 0.75f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            sb.Draw(vaule, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
        }

        private static void DrawFlameGlow(SpriteBatch sb, Rectangle rect, Color glow) {
            Texture2D vaule = VaultAsset.placeholder2.Value;

            //内部填充
            sb.Draw(vaule, rect, new Rectangle(0, 0, 1, 1), glow * 0.2f);

            //发光边框
            int border = 2;
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.7f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - border, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.5f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.6f);
            sb.Draw(vaule, new Rectangle(rect.Right - border, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.6f);
        }
        #endregion
    }
}
