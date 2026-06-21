using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Common
{
    internal static class SkinDrawUtil
    {
        public static void DrawRectBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), new Rectangle(0, 0, 1, 1), color * 0.75f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), new Rectangle(0, 0, 1, 1), color * 0.88f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), new Rectangle(0, 0, 1, 1), color * 0.88f);
        }

        public static void DrawGlowRect(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), color * 0.15f);
            DrawRectBorder(spriteBatch, rect, color * 0.55f, 2);
        }

        public static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }

            edge.Normalize();
            float rotation = edge.ToRotation();
            int segments = Math.Max(1, (int)(length / 10f));
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 pos = start + edge * (length * t);
                spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), Color.Lerp(startColor, endColor, t), rotation, new Vector2(0f, 0.5f), new Vector2(length / segments, thickness), SpriteEffects.None, 0f);
            }
        }

        public static void DrawCornerStar(SpriteBatch spriteBatch, Vector2 pos, float alpha, Color tint) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color color = tint * alpha;
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f, 0.5f), new Vector2(5f, 1.3f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(5f, 1.3f), SpriteEffects.None, 0f);
        }

        public static void DrawPanelShadow(SpriteBatch spriteBatch, Rectangle rect, Color color, int offsetX, int offsetY) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle shadow = rect;
            shadow.Offset(offsetX, offsetY);
            spriteBatch.Draw(pixel, shadow, new Rectangle(0, 0, 1, 1), color);
        }
    }
}
