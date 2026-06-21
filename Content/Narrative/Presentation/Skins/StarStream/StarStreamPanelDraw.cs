using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.StarStream
{
    internal static class StarStreamPanelDraw
    {
        public static void DrawDialogueBackground(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, StarStreamPanelState state) {
            SkinDrawUtil.DrawPanelShadow(spriteBatch, panelRect, new Color(5, 0, 15) * (alpha * 0.6f), 5, 7);
            DrawDeepSpaceGradient(spriteBatch, panelRect, alpha, state.NebulaPulseTimer, 35);
            DrawNebulaOverlay(spriteBatch, panelRect, alpha, state.NebulaPulseTimer);
            DrawAuroraStreaks(spriteBatch, panelRect, alpha * 0.8f, state.AuroraTimer, 5, 40, 1.5f);
            DrawConstellationGrid(spriteBatch, panelRect, alpha * 0.7f, state.ConstellationPhase, 7, 12);
            DrawInnerGoldenGlow(spriteBatch, panelRect, alpha, state.ShimmerTimer);
            DrawStarFrame(spriteBatch, panelRect, alpha, state.ShimmerTimer, fullFrame: true);
        }

        public static void DrawChoiceBackground(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, StarStreamPanelState state) {
            SkinDrawUtil.DrawPanelShadow(spriteBatch, panelRect, new Color(5, 0, 15) * (alpha * 0.6f), 5, 7);
            DrawDeepSpaceGradient(spriteBatch, panelRect, alpha, state.NebulaPulseTimer, 25);
            DrawNebulaOverlay(spriteBatch, panelRect, alpha, state.NebulaPulseTimer);
            DrawConstellationGrid(spriteBatch, panelRect, alpha * 0.6f, state.ConstellationPhase, 5, 8);
            DrawChoiceBorder(spriteBatch, panelRect, GetEdgeColor(alpha, state.ShimmerTimer), state.ShimmerTimer);
        }

        public static void DrawPopupBackground(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float hoverGlow, StarStreamPanelState state) {
            DrawDeepSpaceGradient(spriteBatch, panelRect, alpha * (0.94f + hoverGlow), state.NebulaPulseTimer, 30);
            DrawNebulaOverlay(spriteBatch, panelRect, alpha, state.NebulaPulseTimer);
            DrawConstellationGrid(spriteBatch, panelRect, alpha * 0.7f, state.ConstellationPhase, 5, 10);
            DrawAuroraStreaks(spriteBatch, panelRect, alpha * 0.6f, state.AuroraTimer, 3, 30, 1.2f);
            DrawInnerGoldenGlow(spriteBatch, panelRect, alpha, state.ShimmerTimer, hoverGlow);
        }

        public static void DrawPopupFrame(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow, StarStreamPanelState state) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float pulse = (float)Math.Sin(state.ShimmerTimer * 1.1f) * 0.5f + 0.5f;

            Color outerEdge = Color.Lerp(new Color(180, 140, 50), new Color(240, 200, 100), pulse) * (alpha * (0.8f + hoverGlow * 0.3f));
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.7f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(255, 220, 120) * (alpha * (0.18f + hoverGlow * 0.5f) * pulse);
            spriteBatch.Draw(px, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            spriteBatch.Draw(px, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.65f);
            spriteBatch.Draw(px, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            spriteBatch.Draw(px, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);

            float flowT = (state.ShimmerTimer * 0.8f) % 1f;
            int highlightW = 60;
            int highlightX = rect.X + (int)(flowT * (rect.Width - highlightW));
            Color highlightColor = new Color(255, 230, 140) * (alpha * 0.3f);
            for (int dx = 0; dx < highlightW; dx++) {
                float localT = dx / (float)highlightW;
                float intensity = (float)Math.Sin(localT * MathHelper.Pi);
                spriteBatch.Draw(px, new Rectangle(highlightX + dx, rect.Y, 1, 3), new Rectangle(0, 0, 1, 1), highlightColor * intensity);
            }

            DrawCornerStar(spriteBatch, new Vector2(rect.X + 12, rect.Y + 12), alpha * (0.95f + hoverGlow * 0.4f));
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 12, rect.Y + 12), alpha * (0.95f + hoverGlow * 0.4f));
            DrawCornerStar(spriteBatch, new Vector2(rect.X + 12, rect.Bottom - 12), alpha * (0.6f + hoverGlow * 0.3f));
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 12, rect.Bottom - 12), alpha * (0.6f + hoverGlow * 0.3f));
        }

        public static void DrawChoiceOptionBackground(SpriteBatch spriteBatch, Rectangle choiceRect, bool enabled, float hoverProgress, float alpha, StarStreamPanelState state) {
            Texture2D px = VaultAsset.placeholder2.Value;

            Color choiceBg = enabled
                ? Color.Lerp(new Color(12, 8, 25) * 0.3f, new Color(30, 22, 50) * 0.5f, hoverProgress)
                : new Color(10, 8, 15) * 0.15f;
            spriteBatch.Draw(px, choiceRect, new Rectangle(0, 0, 1, 1), choiceBg * alpha);

            Color goldColor = GetEdgeColor(alpha, state.ShimmerTimer);
            if (enabled && hoverProgress > 0.01f) {
                DrawChoiceOptionBorder(spriteBatch, choiceRect, goldColor * (hoverProgress * 0.6f));

                float shimmer = (float)Math.Sin(state.ShimmerTimer * 3f) * 1.5f;
                Color shimmerColor = goldColor * (hoverProgress * 0.2f);
                spriteBatch.Draw(px,
                    new Rectangle((int)(choiceRect.X + shimmer), choiceRect.Y, 1, choiceRect.Height),
                    new Rectangle(0, 0, 1, 1), shimmerColor);
            }
            else if (!enabled) {
                DrawChoiceOptionBorder(spriteBatch, choiceRect, new Color(50, 40, 30) * (alpha * 0.2f));
            }
        }

        public static void DrawStarGlowRect(SpriteBatch spriteBatch, Rectangle rect, Color glow) {
            Texture2D px = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(px, rect, new Rectangle(0, 0, 1, 1), glow * 0.18f);

            int border = 2;
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.65f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - border, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.45f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.55f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - border, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.55f);
        }

        public static Color GetEdgeColor(float alpha, float shimmerTimer) {
            float pulse = (float)Math.Sin(shimmerTimer * 1.1f) * 0.5f + 0.5f;
            return Color.Lerp(new Color(180, 140, 50), new Color(240, 200, 100), pulse) * (alpha * 0.8f);
        }

        private static void DrawDeepSpaceGradient(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float nebulaPulseTimer, int segments) {
            Texture2D px = VaultAsset.placeholder2.Value;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle band = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                Color deepSpace = new Color(6, 4, 16);
                Color midSpace = new Color(12, 10, 28);
                Color edgeSpace = new Color(22, 18, 45);

                float nebula = (float)Math.Sin(nebulaPulseTimer * 0.5f + t * 1.8f) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(deepSpace, midSpace, nebula);
                Color color = Color.Lerp(blendBase, edgeSpace, t * 0.5f) * (alpha * 0.94f);
                spriteBatch.Draw(px, band, new Rectangle(0, 0, 1, 1), color);
            }
        }

        private static void DrawNebulaOverlay(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float nebulaPulseTimer) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float nebulaPulse = (float)Math.Sin(nebulaPulseTimer * 1.3f) * 0.5f + 0.5f;
            Color nebulaOverlay = new Color(30, 15, 50) * (alpha * 0.2f * nebulaPulse);
            spriteBatch.Draw(px, panelRect, new Rectangle(0, 0, 1, 1), nebulaOverlay);
        }

        private static void DrawInnerGoldenGlow(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float shimmerTimer, float hoverGlow = 0f) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float innerPulse = (float)Math.Sin(shimmerTimer * 1.1f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-5, -5);
            spriteBatch.Draw(px, inner, new Rectangle(0, 0, 1, 1), new Color(200, 160, 60) * (alpha * (0.06f + hoverGlow * 0.4f) * innerPulse));
        }

        private static void DrawAuroraStreaks(SpriteBatch sb, Rectangle rect, float alpha, float auroraTimer, int streakCount, int segments, float thickness) {
            Texture2D px = VaultAsset.placeholder2.Value;
            for (int i = 0; i < streakCount; i++) {
                float t = i / (float)streakCount;
                float baseY = rect.Y + 20 + t * (rect.Height - 40);
                float amplitude = 4f + (float)Math.Sin((auroraTimer + t * 1.5f) * 2f) * 3f;

                Vector2 prevPoint = Vector2.Zero;
                for (int s = 0; s <= segments; s++) {
                    float progress = s / (float)segments;
                    float waveY = baseY + (float)Math.Sin(auroraTimer * 2.5f + progress * MathHelper.TwoPi * 1.2f + t * 2.5f) * amplitude;
                    Vector2 point = new(rect.X + 10 + progress * (rect.Width - 20), waveY);

                    if (s > 0) {
                        Vector2 diff = point - prevPoint;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            Color streakColor = Color.Lerp(new Color(200, 160, 60), new Color(80, 60, 160), progress) * (alpha * 0.06f);
                            sb.Draw(px, prevPoint, new Rectangle(0, 0, 1, 1), streakColor, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prevPoint = point;
                }
            }
        }

        private static void DrawConstellationGrid(SpriteBatch sb, Rectangle rect, float alpha, float constellationPhase, int rows, int horizontalInset) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float rowHeight = rect.Height / (float)rows;

            for (int row = 0; row < rows; row++) {
                float t = row / (float)rows;
                float y = rect.Y + row * rowHeight;
                float phase = constellationPhase + t * MathHelper.Pi;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                Color gridColor = new Color(180, 150, 80) * (alpha * 0.03f * brightness);
                sb.Draw(px, new Rectangle(rect.X + horizontalInset, (int)y, rect.Width - horizontalInset * 2, 1), new Rectangle(0, 0, 1, 1), gridColor);
            }
        }

        private static void DrawStarFrame(SpriteBatch sb, Rectangle rect, float alpha, float shimmerTimer, bool fullFrame) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float pulse = (float)Math.Sin(shimmerTimer * 1.1f) * 0.5f + 0.5f;

            Color outerEdge = Color.Lerp(new Color(180, 140, 50), new Color(240, 200, 100), pulse) * (alpha * 0.8f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.7f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            sb.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerC = new Color(255, 220, 120) * (alpha * 0.18f * pulse);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            sb.Draw(px, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.65f);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.85f);
            sb.Draw(px, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.85f);

            float flowT = (shimmerTimer * 0.8f) % 1f;
            int highlightW = fullFrame ? 80 : 60;
            int highlightX = rect.X + (int)(flowT * (rect.Width - highlightW));
            Color highlightColor = new Color(255, 230, 140) * (alpha * 0.3f);
            for (int dx = 0; dx < highlightW; dx++) {
                float localT = dx / (float)highlightW;
                float intensity = (float)Math.Sin(localT * MathHelper.Pi);
                sb.Draw(px, new Rectangle(highlightX + dx, rect.Y, 1, 3), new Rectangle(0, 0, 1, 1), highlightColor * intensity);
            }

            if (fullFrame) {
                float flowB = ((shimmerTimer * 0.6f) + 0.5f) % 1f;
                int highlightBX = rect.X + (int)((1f - flowB) * (rect.Width - highlightW));
                Color highlightBColor = new Color(255, 210, 100) * (alpha * 0.2f);
                for (int dx = 0; dx < highlightW; dx++) {
                    float localT = dx / (float)highlightW;
                    float intensity = (float)Math.Sin(localT * MathHelper.Pi);
                    sb.Draw(px, new Rectangle(highlightBX + dx, rect.Bottom - 3, 1, 3), new Rectangle(0, 0, 1, 1), highlightBColor * intensity);
                }

                DrawCornerStar(sb, new Vector2(rect.X + 12, rect.Y + 12), alpha * 0.95f);
                DrawCornerStar(sb, new Vector2(rect.Right - 12, rect.Y + 12), alpha * 0.95f);
                DrawCornerStar(sb, new Vector2(rect.X + 12, rect.Bottom - 12), alpha * 0.6f);
                DrawCornerStar(sb, new Vector2(rect.Right - 12, rect.Bottom - 12), alpha * 0.6f);
            }
        }

        private static void DrawChoiceBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, float shimmerTimer) {
            Texture2D px = VaultAsset.placeholder2.Value;

            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), color * 0.7f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), color * 0.9f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), color * 0.9f);

            float flowT = (shimmerTimer * 0.8f) % 1f;
            int highlightW = 60;
            int highlightX = rect.X + (int)(flowT * (rect.Width - highlightW));
            Color highlightColor = new Color(255, 230, 140) * (color.A / 255f * 0.3f);
            for (int dx = 0; dx < highlightW; dx++) {
                float localT = dx / (float)highlightW;
                float intensity = (float)Math.Sin(localT * MathHelper.Pi);
                spriteBatch.Draw(px, new Rectangle(highlightX + dx, rect.Y, 1, 3), new Rectangle(0, 0, 1, 1), highlightColor * intensity);
            }

            DrawCornerStar(spriteBatch, new Vector2(rect.X + 12, rect.Y + 12), color.A / 255f * 0.95f);
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 12, rect.Y + 12), color.A / 255f * 0.95f);
        }

        private static void DrawChoiceOptionBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D px = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), color);
        }

        public static void DrawCornerStar(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 6f;
            Color c = new Color(255, 220, 120) * alpha;

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 1.3f, size * 0.22f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.85f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size * 1.3f, size * 0.22f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.5f, MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.8f, size * 0.18f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.5f, -MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.8f, size * 0.18f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.7f, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 0.35f, size * 0.35f), SpriteEffects.None, 0f);
        }
    }
}
