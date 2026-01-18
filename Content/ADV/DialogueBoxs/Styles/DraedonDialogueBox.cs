using CalamityOverhaul.Content.ADV.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.DialogueBoxs.Styles
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

        #region 样式配置重写

        protected override float PortraitScaleMin => 0.9f;
        protected override float TopNameOffsetBase => 12f;
        protected override float TextBlockOffsetBase => 38f;
        protected override float NameScale => 0.95f;
        protected override float TextScale => 0.82f;
        protected override float NameGlowRadius => 2f;
        protected override float PortraitAvailHeightOffset => 50f;
        protected override float PortraitMinHeight => 100f;
        protected override float PortraitMaxHeight => 270f;
        protected override float PortraitFramePadding => 6f;
        protected override float PortraitGlowPadding => 3f;
        protected override float PortraitLeftMargin => 22f;

        protected override Color GetSilhouetteColor(ContentDrawContext ctx) => new Color(20, 35, 55) * 0.85f;

        protected override Color GetTextLineColor(ContentDrawContext ctx, int lineIndex) {
            return Color.Lerp(new Color(200, 240, 255), Color.White, 0.25f) * ctx.ContentAlpha;
        }

        protected override Vector2 ApplyTextLineOffset(ContentDrawContext ctx, Vector2 basePosition, int lineIndex) {
            float dataShift = (float)Math.Sin(dataStreamTimer * 1.8f + lineIndex * 0.45f) * 0.8f;
            return basePosition + new Vector2(dataShift, 0);
        }

        protected override Color GetContinueHintColor(ContentDrawContext ctx, float blink) {
            return new Color(80, 220, 255) * blink * ctx.ContentAlpha;
        }

        protected override Color GetFastHintColor(ContentDrawContext ctx) {
            return new Color(100, 180, 230) * 0.45f * ctx.ContentAlpha;
        }

        protected override float ContinueHintScale => 0.82f;
        protected override float FastHintScale => 0.72f;

        #endregion

        #region 模板方法实现

        protected override void DrawPortraitFrame(ContentDrawContext ctx, Rectangle frameRect) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            float alpha = ctx.Alpha * ctx.PortraitData.Fade * ctx.PortraitExtraAlpha;

            Color back = new Color(8, 16, 30) * (alpha * 0.88f);
            ctx.SpriteBatch.Draw(vaule, frameRect, new Rectangle(0, 0, 1, 1), back);

            Color edge = new Color(60, 170, 240) * (alpha * 0.65f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Y, frameRect.Width, 3), new Rectangle(0, 0, 1, 1), edge);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Bottom - 3, frameRect.Width, 3), new Rectangle(0, 0, 1, 1), edge * 0.75f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Y, 3, frameRect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.Right - 3, frameRect.Y, 3, frameRect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
        }

        protected override void DrawPortraitGlow(ContentDrawContext ctx, Rectangle glowRect) {
            var pd = ctx.PortraitData;
            Color techRim = new Color(80, 200, 255) * (ctx.ContentAlpha * 0.5f * (float)Math.Sin(circuitPulseTimer * 1.4f + pd.Fade) * pd.Fade + 0.35f * pd.Fade) * ctx.PortraitExtraAlpha;
            DrawDraedonGlowRect(ctx.SpriteBatch, glowRect, techRim);
        }

        protected override void DrawNameGlow(ContentDrawContext ctx, Vector2 position, float alpha) {
            Color nameGlow = new Color(80, 220, 255) * alpha * 0.8f;
            for (int i = 0; i < NameGlowCount; i++) {
                float a = MathHelper.TwoPi * i / NameGlowCount;
                Vector2 off = a.ToRotationVector2() * NameGlowRadius * ctx.SwitchEase;
                Utils.DrawBorderString(ctx.SpriteBatch, current.Speaker, position + off, nameGlow * 0.6f, NameScale);
            }
        }

        protected override void DrawDividerLine(ContentDrawContext ctx, Vector2 start, Vector2 end, float alpha) {
            DrawGradientLine(ctx.SpriteBatch, start, end,
                new Color(60, 160, 240) * (alpha * 0.9f),
                new Color(60, 160, 240) * (alpha * 0.08f),
                1.5f);
        }

        #endregion

        protected override void StyleUpdate(Vector2 panelPos, Vector2 panelSize) {
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

            Rectangle shadow = panelRect;
            shadow.Offset(5, 6);
            spriteBatch.Draw(vaule, shadow, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.65f));

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

            float flicker = (float)Math.Sin(hologramFlicker * 1.5f) * 0.5f + 0.5f;
            Color hologramOverlay = new Color(15, 30, 45) * (alpha * 0.25f * flicker);
            spriteBatch.Draw(vaule, panelRect, new Rectangle(0, 0, 1, 1), hologramOverlay);

            DrawHexGrid(spriteBatch, panelRect, alpha * 0.85f);
            DrawScanLines(spriteBatch, panelRect, alpha * 0.9f);

            float innerPulse = (float)Math.Sin(circuitPulseTimer * 1.3f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-5, -5);
            spriteBatch.Draw(vaule, inner, new Rectangle(0, 0, 1, 1), new Color(40, 180, 255) * (alpha * 0.12f * innerPulse));

            DrawTechFrame(spriteBatch, panelRect, alpha, innerPulse);

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

        private static void DrawDraedonGlowRect(SpriteBatch sb, Rectangle rect, Color glow) {
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
