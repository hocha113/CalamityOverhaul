using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Draedon
{
    internal enum DraedonPanelDetail
    {
        Full,
        Simple
    }

    internal static class DraedonPanelDraw
    {
        private const int NameGlowCount = 4;
        private const float NameGlowRadius = 2f;

        public static void DrawPanel(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, DraedonPanelState state,
            DraedonPanelDetail detail = DraedonPanelDetail.Full, int shadowLayers = 9) {
            Texture2D px = VaultAsset.placeholder2.Value;

            for (int d = shadowLayers; d >= 1; d--) {
                Rectangle shadow = panelRect;
                shadow.Inflate(d, d);
                shadow.Offset(5, 6);
                spriteBatch.Draw(px, shadow, new Rectangle(0, 0, 1, 1),
                    Color.Black * (alpha * 0.055f * (shadowLayers - d + 1) / shadowLayers));
            }

            DrawTerminalBackground(spriteBatch, panelRect, alpha, state);

            if (detail == DraedonPanelDetail.Full) {
                DrawGlitchBar(spriteBatch, panelRect, alpha, state.GlitchTimer);
                DrawScanline(spriteBatch, panelRect, alpha, state.SweepTimer);
            }

            DrawAsymmetricFrame(spriteBatch, panelRect, alpha);

            if (detail == DraedonPanelDetail.Full) {
                DrawRuler(spriteBatch, panelRect, alpha, state.CircuitPulseTimer);
                DrawCornerHex(spriteBatch, panelRect, alpha, state.CornerHex, state.CircuitPulseTimer);
            }
        }

        public static void DrawDashDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha, float dataStreamTimer) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float len = end.X - start.X;
            if (len < 1f) {
                return;
            }

            const int dashW = 5;
            const int gapW = 3;
            float flow = dataStreamTimer * 24f;
            float period = dashW + gapW;
            float x = start.X - (flow % period);
            while (x < end.X) {
                float segStart = Math.Max(x, start.X);
                float segEnd = Math.Min(x + dashW, end.X);
                if (segEnd > segStart) {
                    float t = (segStart - start.X) / len;
                    float bright = MathF.Sin(t * MathHelper.Pi) * 0.45f + 0.55f;
                    Color c = new Color(0, 175, 195) * (alpha * bright * 0.85f);
                    spriteBatch.Draw(px, new Rectangle((int)segStart, (int)start.Y, (int)(segEnd - segStart), 1),
                        new Rectangle(0, 0, 1, 1), c);
                }
                x += period;
            }
        }

        public static void DrawSpeakerGlow(SpriteBatch spriteBatch, Vector2 position, string text, float alpha, float nameScale) {
            Color nameGlow = new Color(0, 220, 200) * (alpha * 0.75f);
            for (int i = 0; i < NameGlowCount; i++) {
                float angle = MathHelper.TwoPi * i / NameGlowCount;
                Vector2 off = angle.ToRotationVector2() * NameGlowRadius;
                Utils.DrawBorderString(spriteBatch, text, position + off, nameGlow * 0.55f, nameScale);
            }

            Utils.DrawBorderString(spriteBatch, ">",
                position - new Vector2(14f, 0f),
                new Color(0, 255, 205) * (alpha * 0.9f),
                nameScale * 0.85f);

            float nameW = FontAssets.MouseText.Value.MeasureString(text).X * nameScale;
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle((int)position.X, (int)(position.Y + 20f), (int)(nameW * 0.65f), 1),
                new Rectangle(0, 0, 1, 1),
                new Color(0, 195, 180) * (alpha * 0.45f));
        }

        public static void DrawPortraitFrame(SpriteBatch spriteBatch, Rectangle frameRect, float alpha, float circuitPulseTimer) {
            Texture2D px = VaultAsset.placeholder2.Value;

            Rectangle glowRect = frameRect;
            glowRect.Inflate(3, 3);
            float pulse = MathF.Sin(circuitPulseTimer * 1.3f) * 0.3f + 0.7f;
            Color glow = new Color(0, 195, 175) * (alpha * 0.5f * pulse);
            spriteBatch.Draw(px, glowRect, new Rectangle(0, 0, 1, 1), glow * 0.13f);
            const int glowBorder = 2;
            spriteBatch.Draw(px, new Rectangle(glowRect.X, glowRect.Y, glowRect.Width, glowBorder), new Rectangle(0, 0, 1, 1), glow * 0.75f);
            spriteBatch.Draw(px, new Rectangle(glowRect.X, glowRect.Bottom - glowBorder, glowRect.Width, glowBorder), new Rectangle(0, 0, 1, 1), glow * 0.45f);
            spriteBatch.Draw(px, new Rectangle(glowRect.X, glowRect.Y, glowBorder, glowRect.Height), new Rectangle(0, 0, 1, 1), glow * 0.60f);
            spriteBatch.Draw(px, new Rectangle(glowRect.Right - glowBorder, glowRect.Y, glowBorder, glowRect.Height), new Rectangle(0, 0, 1, 1), glow * 0.60f);

            spriteBatch.Draw(px, frameRect, new Rectangle(0, 0, 1, 1), new Color(5, 12, 26) * (alpha * 0.92f));

            Color edge = new Color(28, 160, 230) * (alpha * 0.75f);
            const int bw = 2;
            DrawRect(spriteBatch, px, frameRect, bw, edge);

            int cut = Math.Max(4, frameRect.Width / 4);
            for (int row = 0; row < cut; row++) {
                int segLen = cut - row;
                spriteBatch.Draw(px,
                    new Rectangle(frameRect.Right - segLen - bw, frameRect.Y + row, segLen, 1),
                    new Rectangle(0, 0, 1, 1),
                    new Color(5, 12, 26) * alpha);
            }

            Color diagEdge = new Color(0, 210, 205) * (alpha * 0.95f);
            for (int row = 0; row < cut; row++) {
                float fade = 1f - (float)row / cut;
                spriteBatch.Draw(px,
                    new Rectangle(frameRect.Right - (cut - row) - bw, frameRect.Y + row, 2, 1),
                    new Rectangle(0, 0, 1, 1),
                    diagEdge * fade);
            }

            DrawCornerTrace(spriteBatch, px, new Vector2(frameRect.X + bw, frameRect.Bottom - bw), alpha);
        }

        public static void DrawChoiceBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D px = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), color);
        }

        public static void DrawChoiceDashIndicator(SpriteBatch spriteBatch, Rectangle choiceRect, Color techColor,
            float hoverProgress, float alpha, float dataStreamTimer) {
            Texture2D px = VaultAsset.placeholder2.Value;
            const int dashH = 4;
            const int gapH = 3;
            float flow = dataStreamTimer * 20f;
            float period = dashH + gapH;
            Color dashColor = techColor * (hoverProgress * 0.35f);

            float y = choiceRect.Y - (flow % period);
            while (y < choiceRect.Bottom) {
                float segStart = Math.Max(y, choiceRect.Y);
                float segEnd = Math.Min(y + dashH, choiceRect.Bottom);
                if (segEnd > segStart) {
                    spriteBatch.Draw(px,
                        new Rectangle(choiceRect.X, (int)segStart, 2, (int)(segEnd - segStart)),
                        new Rectangle(0, 0, 1, 1), dashColor);
                }
                y += period;
            }
        }

        public static Color GetEdgeColor(float alpha, float hologramFlicker) {
            float flicker = MathF.Sin(hologramFlicker * 1.5f) * 0.5f + 0.5f;
            return Color.Lerp(new Color(0, 175, 195), new Color(0, 220, 210), flicker) * (alpha * 0.85f);
        }

        private static void DrawTerminalBackground(SpriteBatch sb, Rectangle rect, float alpha, DraedonPanelState state) {
            Texture2D px = VaultAsset.placeholder2.Value;
            const int segs = 28;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                float pulse = MathF.Sin(state.CircuitPulseTimer * 0.55f + t * 2.1f) * 0.5f + 0.5f;
                Color dark = new Color(4, 8, 18);
                Color mid = Color.Lerp(new Color(10, 20, 34), new Color(9, 20, 28), t * 0.5f);
                Color c = Color.Lerp(dark, mid, pulse) * (alpha * 0.95f);
                sb.Draw(px, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)),
                    new Rectangle(0, 0, 1, 1), c);
            }

            const int dspacing = 18;
            float dphase = state.DataStreamTimer * 14f;
            for (int col = -(rect.Height / dspacing) - 1; col < (rect.Width / dspacing) + 2; col++) {
                int ox = (int)(col * dspacing + dphase % dspacing);
                for (int row = 0; row < rect.Height; row += 2) {
                    int px2 = rect.X + ox - row;
                    if (px2 < rect.X || px2 >= rect.Right) {
                        continue;
                    }
                    sb.Draw(px, new Rectangle(px2, rect.Y + row, 1, 1),
                        new Rectangle(0, 0, 1, 1), new Color(18, 72, 82) * (alpha * 0.032f));
                }
            }

            float flicker = MathF.Sin(state.HologramFlicker * 1.6f) * 0.5f + 0.5f;
            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1), new Color(0, 28, 36) * (alpha * 0.18f * flicker));
        }

        private static void DrawGlitchBar(SpriteBatch sb, Rectangle rect, float alpha, float glitchTimer) {
            float gf = MathF.Sin(glitchTimer * 2.1f);
            if (gf <= 0.97f) {
                return;
            }

            float gy = rect.Y + (glitchTimer * 97f % rect.Height);
            sb.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(rect.X + 5, (int)gy, rect.Width - 10, 1),
                new Rectangle(0, 0, 1, 1),
                new Color(0, 200, 195) * (alpha * (gf - 0.97f) * 3.5f));
        }

        private static void DrawScanline(SpriteBatch sb, Rectangle rect, float alpha, float sweepTimer) {
            float scanY = rect.Y + sweepTimer * rect.Height;
            for (int row = 0; row <= 3; row++) {
                float iy = scanY + row * 1.5f;
                if (iy < rect.Y || iy > rect.Bottom) {
                    continue;
                }
                float fade = 1f - row * 0.28f;
                sb.Draw(VaultAsset.placeholder2.Value,
                    new Rectangle(rect.X + 8, (int)iy, rect.Width - 16, 1),
                    new Rectangle(0, 0, 1, 1),
                    new Color(0, 185, 200) * (alpha * 0.2f * fade));
            }
        }

        private static void DrawAsymmetricFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color topBright = new Color(0, 218, 208) * (alpha * 0.97f);
            Color topDim = new Color(0, 140, 160) * (alpha * 0.45f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), topBright);
            sb.Draw(px, new Rectangle(rect.X, rect.Y + 3, rect.Width, 1), new Rectangle(0, 0, 1, 1), topDim);

            int lbH = rect.Height / 2;
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 4, lbH), new Rectangle(0, 0, 1, 1), new Color(0, 200, 190) * (alpha * 0.72f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y + lbH, 4, rect.Height - lbH), new Rectangle(0, 0, 1, 1), new Color(0, 130, 130) * (alpha * 0.35f));
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), new Color(0, 95, 115) * (alpha * 0.42f));
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Rectangle(0, 0, 1, 1), new Color(0, 115, 135) * (alpha * 0.32f));

            sb.Draw(px, new Rectangle(rect.X + 4, rect.Y, 1, 9), new Rectangle(0, 0, 1, 1), topBright * 0.82f);
            sb.Draw(px, new Rectangle(rect.X + 18, rect.Y, 1, 6), new Rectangle(0, 0, 1, 1), topBright * 0.55f);
            sb.Draw(px, new Rectangle(rect.X + 32, rect.Y, 1, 4), new Rectangle(0, 0, 1, 1), topBright * 0.32f);
        }

        private static void DrawRuler(SpriteBatch sb, Rectangle rect, float alpha, float circuitPulseTimer) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int rx = rect.Right - 10;
            const int spacing = 12;
            int marks = rect.Height / spacing;
            float flow = circuitPulseTimer * 0.25f;
            for (int i = 0; i < marks; i++) {
                float t = (float)i / marks;
                float bright = MathF.Sin((t + flow) * MathHelper.TwoPi) * 0.3f + 0.45f;
                int mLen = (i % 4 == 0) ? 7 : 4;
                Color mc = new Color(0, 175, 168) * (alpha * bright);
                sb.Draw(px, new Rectangle(rx - mLen, rect.Y + i * spacing, mLen, 1), new Rectangle(0, 0, 1, 1), mc);
            }
        }

        private static void DrawCornerHex(SpriteBatch sb, Rectangle rect, float alpha, string[] cornerHex, float circuitPulseTimer) {
            if (alpha < 0.04f) {
                return;
            }

            float blink = MathF.Sin(circuitPulseTimer * 0.75f) * 0.18f + 0.82f;
            Color col = new Color(0, 155, 148) * (alpha * 0.55f * blink);
            const float sc = 0.55f;
            var font = FontAssets.MouseText.Value;

            Utils.DrawBorderString(sb, cornerHex[0], new Vector2(rect.X + 6f, rect.Y + 6f), col, sc);
            float w1 = font.MeasureString(cornerHex[1]).X * sc;
            Utils.DrawBorderString(sb, cornerHex[1], new Vector2(rect.Right - w1 - 14f, rect.Y + 6f), col, sc);
            Utils.DrawBorderString(sb, cornerHex[2], new Vector2(rect.X + 6f, rect.Bottom - 16f), col * 0.68f, sc);
            float w3 = font.MeasureString(cornerHex[3]).X * sc;
            Utils.DrawBorderString(sb, cornerHex[3], new Vector2(rect.Right - w3 - 14f, rect.Bottom - 16f), col * 0.68f, sc);
        }

        private static void DrawRect(SpriteBatch sb, Texture2D px, Rectangle r, int bw, Color c) {
            sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, bw), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(px, new Rectangle(r.X, r.Bottom - bw, r.Width, bw), new Rectangle(0, 0, 1, 1), c * 0.7f);
            sb.Draw(px, new Rectangle(r.X, r.Y, bw, r.Height), new Rectangle(0, 0, 1, 1), c * 0.85f);
            sb.Draw(px, new Rectangle(r.Right - bw, r.Y, bw, r.Height), new Rectangle(0, 0, 1, 1), c * 0.85f);
        }

        private static void DrawCornerTrace(SpriteBatch sb, Texture2D px, Vector2 origin, float alpha) {
            Color c = new Color(0, 175, 195) * (alpha * 0.62f);
            sb.Draw(px, new Rectangle((int)origin.X, (int)origin.Y - 1, 16, 1), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(px, new Rectangle((int)origin.X + 14, (int)origin.Y - 5, 2, 4), new Rectangle(0, 0, 1, 1), c * 0.65f);
        }
    }
}
