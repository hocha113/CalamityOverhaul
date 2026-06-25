using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Sulfsea
{
    internal static class SulfseaPanelDraw
    {
        /// <summary>硫磺海面板背景：优先 SulfseaPanel 着色器，着色器缺失时回退到 CPU 色带绘制</summary>
        public static void DrawShaderBackground(SpriteBatch spriteBatch, Rectangle rect, float alpha, SulfseaPanelState state, float hoverGlow = 0f) {
            SkinDrawUtil.DrawPanelShadow(spriteBatch, rect, Color.Black * (alpha * 0.60f), 6, 8);

            if (SulfseaShaderPanel.Available) {
                float miasma01 = (float)Math.Sin(state.MiasmaTimer * 1.1f) * 0.5f + 0.5f;
                float bright = MathHelper.Clamp(0.95f + hoverGlow * 0.35f, 0f, 1.4f);
                Color tint = new(
                    (byte)Math.Min(255, (int)(228 * bright)),
                    (byte)Math.Min(255, (int)(236 * bright)),
                    (byte)Math.Min(255, (int)(200 * bright)));
                SulfseaShaderPanel.Draw(spriteBatch, rect, alpha * 0.97f, miasma01, state.ShaderTime, SulfseaPanelState.ShaderEdgePad, tint);
            }
            else {
                DrawCpuBackground(spriteBatch, rect, alpha, state.ToxicWavePhase, state.SulfurPulse, state.MiasmaTimer, hoverGlow);
            }
        }

        private static void DrawCpuBackground(SpriteBatch spriteBatch, Rectangle rect, float alpha, float toxicWavePhase, float sulfurPulse, float miasmaTimer, float hoverGlow) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            const int segments = 30;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Rectangle band = new(rect.X, y1, rect.Width, Math.Max(1, y2 - y1));
                Color sulfurDeep = new(12, 18, 8);
                Color toxicMid = new(28, 38, 15);
                Color acidEdge = new(65, 85, 30);
                float breathing = (float)Math.Sin(sulfurPulse) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(sulfurDeep, toxicMid, (float)Math.Sin(sulfurPulse * 0.5f + t * 1.4f) * 0.5f + 0.5f);
                Color color = Color.Lerp(blendBase, acidEdge, t * 0.7f * (0.3f + breathing * 0.7f));
                spriteBatch.Draw(pixel, band, new Rectangle(0, 0, 1, 1), color * alpha * (0.92f + hoverGlow));
            }

            float miasma = (float)Math.Sin(miasmaTimer * 1.1f) * 0.5f + 0.5f;
            spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), new Color(45, 55, 20) * (alpha * (0.4f + hoverGlow * 0.2f) * miasma));
            DrawToxicWaveOverlay(spriteBatch, rect, alpha * (0.85f + hoverGlow * 0.15f), toxicWavePhase);

            float pulse = (float)Math.Sin(sulfurPulse * 2.2f) * 0.5f + 0.5f;
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            spriteBatch.Draw(pixel, inner, new Rectangle(0, 0, 1, 1), new Color(80, 100, 35) * (alpha * (0.09f + hoverGlow * 0.4f) * (0.5f + pulse * 0.5f)));
        }

        private static void DrawToxicWaveOverlay(SpriteBatch spriteBatch, Rectangle rect, float alpha, float toxicWavePhase) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            const int bands = 6;
            for (int i = 0; i < bands; i++) {
                float t = i / (float)bands;
                float y = rect.Y + 18 + t * (rect.Height - 36);
                float amp = 7f + (float)Math.Sin((toxicWavePhase + t) * 2.2f) * 4.5f;
                const int segments = 42;
                Vector2 prev = Vector2.Zero;
                for (int s = 0; s <= segments; s++) {
                    float p = s / (float)segments;
                    float localY = y + (float)Math.Sin(toxicWavePhase * 2.2f + p * MathHelper.TwoPi * 1.3f + t) * amp;
                    Vector2 point = new(rect.X + 8 + p * (rect.Width - 16), localY);
                    if (s > 0) {
                        Vector2 diff = point - prev;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            spriteBatch.Draw(pixel, prev, new Rectangle(0, 0, 1, 1), new Color(60, 90, 30) * (alpha * 0.08f), diff.ToRotation(), Vector2.Zero, new Vector2(len, 2.2f), SpriteEffects.None, 0f);
                        }
                    }
                    prev = point;
                }
            }
        }

        /// <summary>面板描边与四角星：着色器内边之上的清晰前景细节</summary>
        public static void DrawFrame(SpriteBatch spriteBatch, Rectangle rect, float alpha, float pulse) {
            Color edge = Color.Lerp(new Color(70, 100, 35), new Color(130, 160, 65), pulse) * (alpha * 0.85f);
            SkinDrawUtil.DrawRectBorder(spriteBatch, rect, edge, 2);
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            SkinDrawUtil.DrawRectBorder(spriteBatch, inner, new Color(140, 170, 70) * (alpha * 0.22f * pulse), 1);
            Color starTint = new(160, 190, 80);
            SkinDrawUtil.DrawCornerStar(spriteBatch, new Vector2(rect.X + 10, rect.Y + 10), alpha * 0.9f, starTint);
            SkinDrawUtil.DrawCornerStar(spriteBatch, new Vector2(rect.Right - 10, rect.Y + 10), alpha * 0.9f, starTint);
            SkinDrawUtil.DrawCornerStar(spriteBatch, new Vector2(rect.X + 10, rect.Bottom - 10), alpha * 0.65f, starTint);
            SkinDrawUtil.DrawCornerStar(spriteBatch, new Vector2(rect.Right - 10, rect.Bottom - 10), alpha * 0.65f, starTint);
        }

        /// <summary>仅绘制四角星：对话/选项皮肤在着色器内边之上的轻量签名细节</summary>
        public static void DrawCornerStars(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Color starTint = new(160, 190, 80);
            SkinDrawUtil.DrawCornerStar(spriteBatch, new Vector2(rect.X + 10, rect.Y + 10), alpha * 0.9f, starTint);
            SkinDrawUtil.DrawCornerStar(spriteBatch, new Vector2(rect.Right - 10, rect.Y + 10), alpha * 0.9f, starTint);
            SkinDrawUtil.DrawCornerStar(spriteBatch, new Vector2(rect.X + 10, rect.Bottom - 10), alpha * 0.65f, starTint);
            SkinDrawUtil.DrawCornerStar(spriteBatch, new Vector2(rect.Right - 10, rect.Bottom - 10), alpha * 0.65f, starTint);
        }
    }
}
