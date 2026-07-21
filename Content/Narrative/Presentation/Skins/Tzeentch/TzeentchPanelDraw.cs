using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Tzeentch
{
    internal static class TzeentchPanelDraw
    {
        /// <summary>面板背景,TzeentchPanel 优先否则 CPU</summary>
        public static void DrawShaderBackground(SpriteBatch spriteBatch, Rectangle rect, float alpha, TzeentchPanelState state, float hoverGlow = 0f) {
            SkinDrawUtil.DrawPanelShadow(spriteBatch, rect, Color.Black * (alpha * 0.60f), 6, 8);

            if (TzeentchShaderPanel.Available) {
                float warp01 = MathHelper.Clamp(state.Warp01 + hoverGlow * 0.4f, 0f, 1f);
                float bright = MathHelper.Clamp(0.96f + hoverGlow * 0.35f, 0f, 1.4f);
                Color tint = new(
                    (byte)Math.Min(255, (int)(225 * bright)),
                    (byte)Math.Min(255, (int)(222 * bright)),
                    (byte)Math.Min(255, (int)(245 * bright)));
                TzeentchShaderPanel.Draw(spriteBatch, rect, alpha * 0.97f, warp01, state.ShaderTime, TzeentchPanelState.ShaderEdgePad, tint);
            }
            else {
                DrawCpuBackground(spriteBatch, rect, alpha, state, hoverGlow);
            }
        }

        private static void DrawCpuBackground(SpriteBatch spriteBatch, Rectangle rect, float alpha, TzeentchPanelState state, float hoverGlow) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            const int segments = 30;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Rectangle band = new(rect.X, y1, rect.Width, Math.Max(1, y2 - y1));
                Color baseC = Color.Lerp(TzeentchPalette.Void, TzeentchPalette.Deep, t);
                baseC = Color.Lerp(baseC, TzeentchPalette.DeepEdge, t * 0.45f);
                spriteBatch.Draw(pixel, band, new Rectangle(0, 0, 1, 1), baseC * alpha * (0.92f + hoverGlow));
            }

            float miasma = (float)Math.Sin(state.WarpTimer * 1.1f) * 0.5f + 0.5f;
            Color hue = TzeentchPalette.Cycle(state.SchemePulse * 0.05f);
            spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), hue * (alpha * (0.12f + hoverGlow * 0.18f) * miasma));

            DrawCpuThreads(spriteBatch, rect, alpha * (0.85f + hoverGlow * 0.15f), state.WarpTimer);

            float pulse = (float)Math.Sin(state.SchemePulse * 2.2f) * 0.5f + 0.5f;
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            spriteBatch.Draw(pixel, inner, new Rectangle(0, 0, 1, 1), TzeentchPalette.Gold * (alpha * (0.07f + hoverGlow * 0.35f) * (0.5f + pulse * 0.5f)));
        }

        //CPU回退用的廉价命运金线
        private static void DrawCpuThreads(SpriteBatch spriteBatch, Rectangle rect, float alpha, float wavePhase) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            const int bands = 5;
            for (int i = 0; i < bands; i++) {
                float t = i / (float)bands;
                float y = rect.Y + 20 + t * (rect.Height - 40);
                float amp = 6f + (float)Math.Sin((wavePhase + t) * 2.0f) * 4f;
                const int segments = 40;
                Vector2 prev = Vector2.Zero;
                for (int s = 0; s <= segments; s++) {
                    float p = s / (float)segments;
                    float localY = y + (float)Math.Sin(wavePhase * 1.8f + p * MathHelper.TwoPi * 1.3f + t) * amp;
                    Vector2 point = new(rect.X + 8 + p * (rect.Width - 16), localY);
                    if (s > 0) {
                        Vector2 diff = point - prev;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            spriteBatch.Draw(pixel, prev, new Rectangle(0, 0, 1, 1), TzeentchPalette.Gold * (alpha * 0.07f), diff.ToRotation(), Vector2.Zero, new Vector2(len, 1.6f), SpriteEffects.None, 0f);
                        }
                    }
                    prev = point;
                }
            }
        }

        /// <summary>描边与四角符印</summary>
        public static void DrawFrame(SpriteBatch spriteBatch, Rectangle rect, float alpha, float pulse) {
            Color edge = Color.Lerp(TzeentchPalette.Violet, TzeentchPalette.Gold, pulse) * (alpha * 0.85f);
            SkinDrawUtil.DrawRectBorder(spriteBatch, rect, edge, 2);
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            SkinDrawUtil.DrawRectBorder(spriteBatch, inner, TzeentchPalette.Gold * (alpha * 0.22f * pulse), 1);
            DrawCornerSigils(spriteBatch, rect, alpha);
        }

        /// <summary>仅四角符印</summary>
        public static void DrawCornerSigils(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            DrawSigil(spriteBatch, new Vector2(rect.X + 11, rect.Y + 11), alpha * 0.95f);
            DrawSigil(spriteBatch, new Vector2(rect.Right - 11, rect.Y + 11), alpha * 0.95f);
            DrawSigil(spriteBatch, new Vector2(rect.X + 11, rect.Bottom - 11), alpha * 0.65f);
            DrawSigil(spriteBatch, new Vector2(rect.Right - 11, rect.Bottom - 11), alpha * 0.65f);
        }

        //八芒符印,正交+斜十字
        private static void DrawSigil(SpriteBatch spriteBatch, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color c = TzeentchPalette.Gold * alpha;
            const float arm = 6f;
            spriteBatch.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(arm * 1.3f, 1.2f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.85f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(arm * 1.3f, 1.2f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.5f, MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(arm * 0.8f, 1.0f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.5f, -MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(arm * 0.8f, 1.0f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, pos, new Rectangle(0, 0, 1, 1), Color.Lerp(c, Color.White, 0.6f), 0f, new Vector2(0.5f, 0.5f), new Vector2(1.6f), SpriteEffects.None, 0f);
        }
    }
}
