using CalamityOverhaul.Content.ADV.UIEffect;
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
    /// 特化的嘉登科技风格 GalGame 对话框
    /// </summary>
    internal class DraedonDialogueBox : DialogueBoxBase
    {
        public static DraedonDialogueBox Instance => UIHandleLoader.GetUIHandleOfType<DraedonDialogueBox>();
        public override string LocalizationCategory => "UI";

        //风格参数
        private const float FixedWidth = 540f;
        protected override float PanelWidth => FixedWidth;

        //背景动画参数
        private float scanLineTimer = 0f;
        private float hologramFlicker = 0f;
        private float circuitPulseTimer = 0f;
        private float dataStreamTimer = 0f;
        private float hexGridPhase = 0f;

        //视觉粒子
        private readonly List<DraedonDataPRT> dataParticles = [];
        private int dataParticleSpawnTimer = 0;
        private readonly List<CircuitNodePRT> circuitNodes = [];
        private int circuitNodeSpawnTimer = 0;
        private const float TechSideMargin = 28f;

        protected override void StyleUpdate(Vector2 panelPos, Vector2 panelSize) {
            //背景动画计时器
            scanLineTimer += 0.048f;
            hologramFlicker += 0.12f;
            circuitPulseTimer += 0.025f;
            dataStreamTimer += 0.055f;
            hexGridPhase += 0.015f;

            if (scanLineTimer > MathHelper.TwoPi) scanLineTimer -= MathHelper.TwoPi;
            if (hologramFlicker > MathHelper.TwoPi) hologramFlicker -= MathHelper.TwoPi;
            if (circuitPulseTimer > MathHelper.TwoPi) circuitPulseTimer -= MathHelper.TwoPi;
            if (dataStreamTimer > MathHelper.TwoPi) dataStreamTimer -= MathHelper.TwoPi;
            if (hexGridPhase > MathHelper.TwoPi) hexGridPhase -= MathHelper.TwoPi;

            //数据粒子刷新
            dataParticleSpawnTimer++;
            if (Active && dataParticleSpawnTimer >= 18 && dataParticles.Count < 15) {
                dataParticleSpawnTimer = 0;
                Vector2 p = panelPos + new Vector2(Main.rand.NextFloat(TechSideMargin, panelSize.X - TechSideMargin), Main.rand.NextFloat(40f, panelSize.Y - 40f));
                dataParticles.Add(new DraedonDataPRT(p));
            }
            for (int i = dataParticles.Count - 1; i >= 0; i--) {
                if (dataParticles[i].Update(panelPos, panelSize)) {
                    dataParticles.RemoveAt(i);
                }
            }

            //电路节点刷新
            circuitNodeSpawnTimer++;
            if (Active && circuitNodeSpawnTimer >= 25 && circuitNodes.Count < 8) {
                circuitNodeSpawnTimer = 0;
                float scaleW = Main.UIScale;
                float left = panelPos.X + TechSideMargin * scaleW;
                float right = panelPos.X + panelSize.X - TechSideMargin * scaleW;
                Vector2 start = new(Main.rand.NextFloat(left, right), panelPos.Y + Main.rand.NextFloat(40f, panelSize.Y - 40f));
                circuitNodes.Add(new CircuitNodePRT(start));
            }
            for (int i = circuitNodes.Count - 1; i >= 0; i--) {
                if (circuitNodes[i].Update(panelPos, panelSize)) {
                    circuitNodes.RemoveAt(i);
                }
            }
        }

        protected override void DrawStyle(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha, float easedProgress) {
            Texture2D vaule = VaultAsset.placeholder2.Value;

            //阴影
            Rectangle shadow = panelRect;
            shadow.Offset(5, 6);
            spriteBatch.Draw(vaule, shadow, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.65f));

            //主背景渐变
            int segs = 35;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                Color techDark = new Color(8, 12, 22);
                Color techMid = new Color(18, 28, 42);
                Color techEdge = new Color(35, 55, 85);

                float pulse = (float)Math.Sin(circuitPulseTimer * 0.6f + t * 2.0f) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(techDark, techMid, pulse);
                Color c = Color.Lerp(blendBase, techEdge, t * 0.45f);
                c *= alpha * 0.92f;

                spriteBatch.Draw(vaule, r, new Rectangle(0, 0, 1, 1), c);
            }

            //全息闪烁覆盖层
            float flicker = (float)Math.Sin(hologramFlicker * 1.5f) * 0.5f + 0.5f;
            Color hologramOverlay = new Color(15, 30, 45) * (alpha * 0.25f * flicker);
            spriteBatch.Draw(vaule, panelRect, new Rectangle(0, 0, 1, 1), hologramOverlay);

            //六角网格纹理
            DrawHexGrid(spriteBatch, panelRect, alpha * 0.85f);

            //扫描线效果
            DrawScanLines(spriteBatch, panelRect, alpha * 0.9f);

            //电路脉冲内发光
            float innerPulse = (float)Math.Sin(circuitPulseTimer * 1.3f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-5, -5);
            spriteBatch.Draw(vaule, inner, new Rectangle(0, 0, 1, 1), new Color(40, 180, 255) * (alpha * 0.12f * innerPulse));

            //科技边框
            DrawTechFrame(spriteBatch, panelRect, alpha, innerPulse);

            //绘制粒子
            foreach (var node in circuitNodes) {
                node.Draw(spriteBatch, alpha * 0.85f);
            }
            foreach (var particle in dataParticles) {
                particle.Draw(spriteBatch, alpha * 0.75f);
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

            float portraitAppearScale = MathHelper.Lerp(0.9f, 1f, switchEase);
            float portraitExtraAlpha = MathHelper.Clamp(switchEase, 0f, 1f);

            float leftOffset = Padding;
            float topNameOffset = 12f;
            float textBlockOffsetY = Padding + 38;

            if (hasPortrait) {
                float availHeight = panelRect.Height - 50f;
                float maxPortraitHeight = Math.Clamp(availHeight, 100f, 270f);
                Texture2D ptex = speakerPortrait.Texture;
                float scaleBase = Math.Min(PortraitWidth / ptex.Width, maxPortraitHeight / ptex.Height);
                float scale = scaleBase * portraitAppearScale;
                Vector2 pSize = ptex.Size() * scale;
                Vector2 pPos = new(panelRect.X + Padding + PortraitInnerPadding, panelRect.Y + panelRect.Height - pSize.Y - Padding - 8f);

                DrawPortraitFrame(spriteBatch, new Rectangle((int)(pPos.X - 6), (int)(pPos.Y - 6), (int)(pSize.X + 12), (int)(pSize.Y + 12)), alpha * speakerPortrait.Fade * portraitExtraAlpha);

                Color drawColor = speakerPortrait.BaseColor * contentAlpha * speakerPortrait.Fade * portraitExtraAlpha;
                if (speakerPortrait.Silhouette) {
                    drawColor = new Color(20, 35, 55) * (contentAlpha * speakerPortrait.Fade * portraitExtraAlpha) * 0.85f;
                }

                spriteBatch.Draw(ptex, pPos, null, drawColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                Color techRim = new Color(80, 200, 255) * (contentAlpha * 0.5f * (float)Math.Sin(circuitPulseTimer * 1.4f + speakerPortrait.Fade) * speakerPortrait.Fade + 0.35f * speakerPortrait.Fade) * portraitExtraAlpha;
                DrawGlowRect(spriteBatch, new Rectangle((int)pPos.X - 3, (int)pPos.Y - 3, (int)pSize.X + 6, (int)pSize.Y + 6), techRim);

                leftOffset += PortraitWidth + 22f;
            }

            if (current != null && !string.IsNullOrEmpty(current.Speaker)) {
                Vector2 speakerPos = new(panelRect.X + leftOffset, panelRect.Y + topNameOffset - (1f - switchEase) * 5f);
                float nameAlpha = contentAlpha * switchEase;

                Color nameGlow = new Color(80, 220, 255) * nameAlpha * 0.8f;
                for (int i = 0; i < 4; i++) {
                    float a = MathHelper.TwoPi * i / 4f;
                    Vector2 off = a.ToRotationVector2() * 2f * switchEase;
                    Utils.DrawBorderString(spriteBatch, current.Speaker, speakerPos + off, nameGlow * 0.6f, 0.95f);
                }

                Utils.DrawBorderString(spriteBatch, current.Speaker, speakerPos, Color.White * nameAlpha, 0.95f);

                Vector2 divStart = speakerPos + new Vector2(0, 28);
                Vector2 divEnd = divStart + new Vector2(panelRect.Width - leftOffset - Padding, 0);
                float lineAlpha = contentAlpha * switchEase;
                DrawGradientLine(spriteBatch, divStart, divEnd, new Color(60, 160, 240) * (lineAlpha * 0.9f), new Color(60, 160, 240) * (lineAlpha * 0.08f), 1.5f);
            }

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

                float dataShift = (float)Math.Sin(dataStreamTimer * 1.8f + i * 0.45f) * 0.8f;
                Vector2 shiftedPos = linePos + new Vector2(dataShift, 0);

                Color lineColor = Color.Lerp(new Color(200, 240, 255), Color.White, 0.25f) * contentAlpha;
                Utils.DrawBorderString(spriteBatch, visLine, shiftedPos, lineColor, 0.82f);
            }

            if (waitingForAdvance) {
                float blink = (float)Math.Sin(advanceBlinkTimer / 11f * MathHelper.TwoPi) * 0.5f + 0.5f;
                string hint = $"> {ContinueHint.Value}<";
                Vector2 hintSize = font.MeasureString(hint) * 0.65f;
                Vector2 hintPos = new(panelRect.Right - Padding - hintSize.X, panelRect.Bottom - Padding - hintSize.Y);
                Utils.DrawBorderString(spriteBatch, hint, hintPos, new Color(80, 220, 255) * blink * contentAlpha, 0.82f);
            }

            if (!finishedCurrent) {
                string fast = FastHint.Value;
                Vector2 fastSize = font.MeasureString(fast) * 0.62f;
                Vector2 fastPos = new(panelRect.Right - Padding - fastSize.X, panelRect.Bottom - Padding - fastSize.Y - 18);
                Utils.DrawBorderString(spriteBatch, fast, fastPos, new Color(100, 180, 230) * 0.45f * contentAlpha, 0.72f);
            }
        }

        #region 样式工具函数
        private void DrawHexGrid(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            int hexRows = 8;
            float hexHeight = rect.Height / (float)hexRows;

            for (int row = 0; row < hexRows; row++) {
                float t = row / (float)hexRows;
                float y = rect.Y + row * hexHeight;
                float phase = hexGridPhase + t * MathHelper.Pi;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                Color gridColor = new Color(25, 90, 140) * (alpha * 0.04f * brightness);
                sb.Draw(vaule, new Rectangle(rect.X + 10, (int)y, rect.Width - 20, 1), new Rectangle(0, 0, 1, 1), gridColor);
            }
        }

        private void DrawScanLines(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            float scanY = rect.Y + (float)Math.Sin(scanLineTimer) * 0.5f * rect.Height + rect.Height * 0.5f;

            for (int i = -2; i <= 2; i++) {
                float offsetY = scanY + i * 3f;
                if (offsetY < rect.Y || offsetY > rect.Bottom) {
                    continue;
                }

                float intensity = 1f - Math.Abs(i) * 0.3f;
                Color scanColor = new Color(60, 180, 255) * (alpha * 0.15f * intensity);
                sb.Draw(vaule, new Rectangle(rect.X + 8, (int)offsetY, rect.Width - 16, 2), new Rectangle(0, 0, 1, 1), scanColor);
            }
        }

        private static void DrawTechFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            Color techEdge = Color.Lerp(new Color(40, 160, 240), new Color(80, 200, 255), pulse) * (alpha * 0.85f);

            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), techEdge);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), techEdge * 0.75f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), techEdge * 0.9f);
            sb.Draw(vaule, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), techEdge * 0.9f);

            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerC = new Color(100, 200, 255) * (alpha * 0.22f * pulse);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.7f);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.9f);
            sb.Draw(vaule, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.9f);

            DrawCornerCircuit(sb, new Vector2(rect.X + 12, rect.Y + 12), alpha * 0.95f);
            DrawCornerCircuit(sb, new Vector2(rect.Right - 12, rect.Y + 12), alpha * 0.95f);
            DrawCornerCircuit(sb, new Vector2(rect.X + 12, rect.Bottom - 12), alpha * 0.65f);
            DrawCornerCircuit(sb, new Vector2(rect.Right - 12, rect.Bottom - 12), alpha * 0.65f);
        }

        private static void DrawCornerCircuit(SpriteBatch sb, Vector2 pos, float a) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 6f;
            Color c = new Color(100, 220, 255) * a;

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.85f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.6f, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 0.4f, size * 0.4f), SpriteEffects.None, 0f);
        }

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }

            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 12f));

            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }
        #endregion

        #region 公共静态绘制碎片
        private static void DrawPortraitFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            Color back = new Color(8, 16, 30) * (alpha * 0.88f);
            sb.Draw(vaule, rect, new Rectangle(0, 0, 1, 1), back);

            Color edge = new Color(60, 170, 240) * (alpha * 0.65f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), edge * 0.75f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            sb.Draw(vaule, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
        }

        private static void DrawGlowRect(SpriteBatch sb, Rectangle rect, Color glow) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            sb.Draw(vaule, rect, new Rectangle(0, 0, 1, 1), glow * 0.18f);

            int border = 2;
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.65f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - border, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.45f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.55f);
            sb.Draw(vaule, new Rectangle(rect.Right - border, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.55f);
        }
        #endregion
    }
}
