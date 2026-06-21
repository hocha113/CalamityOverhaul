using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Brimstone
{
    internal static class BrimstonePanelDraw
    {
        public static void DrawShaderBackground(SpriteBatch spriteBatch, Rectangle rect, float alpha, BrimstonePanelState state, float hoverGlow = 0f) {
            SkinDrawUtil.DrawPanelShadow(spriteBatch, rect, new Color(20, 0, 0) * (alpha * 0.65f), 7, 9);
            float pulse01 = (float)Math.Sin(state.InfernoPulse * 1.8f) * 0.5f + 0.5f;
            float bright = MathHelper.Clamp(0.95f + hoverGlow * 0.30f, 0f, 1.4f);
            Color tint = new Color(
                (byte)Math.Min(255, (int)(255 * bright)),
                (byte)Math.Min(255, (int)(238 * bright)),
                (byte)Math.Min(255, (int)(220 * bright)),
                255);
            BrimstoneShaderPanel.Draw(spriteBatch, rect, alpha * 0.97f, pulse01, state.ShaderTime, BrimstonePanelState.ShaderEdgePad, tint);
        }

        public static void DrawFlameBorder(SpriteBatch spriteBatch, Rectangle rect, Color edge) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), edge);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), edge * 0.75f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.9f);
        }

        public static void DrawFlameGlow(SpriteBatch spriteBatch, Rectangle rect, Color glow) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), glow * 0.2f);
            int border = 2;
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - border, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.5f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.6f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - border, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.6f);
        }

        public static void DrawPopupFrame(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow, BrimstonePanelState state) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float pulse = (float)Math.Sin(state.EmberGlowTimer * 1.5f) * 0.5f + 0.5f;
            Color outerEdge = Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), pulse) * (alpha * (0.85f + hoverGlow * 0.3f));
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(220, 100, 50) * (alpha * (0.22f + hoverGlow * 0.5f) * pulse);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            spriteBatch.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);

            DrawFlameMark(spriteBatch, new Vector2(rect.X + 12, rect.Y + 12), alpha * (0.95f + hoverGlow * 0.4f));
            DrawFlameMark(spriteBatch, new Vector2(rect.Right - 12, rect.Y + 12), alpha * (0.95f + hoverGlow * 0.4f));
            DrawFlameMark(spriteBatch, new Vector2(rect.X + 12, rect.Bottom - 12), alpha * (0.65f + hoverGlow * 0.3f));
            DrawFlameMark(spriteBatch, new Vector2(rect.Right - 12, rect.Bottom - 12), alpha * (0.65f + hoverGlow * 0.3f));
        }

        private static void DrawFlameMark(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            const float size = 6f;
            Color flameColor = new Color(255, 150, 70) * alpha;
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), flameColor, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 1.2f, size * 0.3f), SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.85f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size * 1.2f, size * 0.3f), SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.7f, MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.9f, size * 0.25f), SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.7f, -MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.9f, size * 0.25f), SpriteEffects.None, 0f);
        }
    }
}
