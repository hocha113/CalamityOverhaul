using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.ADVChoiceBoxs.Styles
{
    /// <summary>
    /// 默认深蓝科技风格
    /// </summary>
    internal class DefaultChoiceBoxStyle : IChoiceBoxStyle
    {
        private float styleAnimTimer = 0f;

        public void Update(Rectangle panelRect, bool active, bool closing) {
            styleAnimTimer += 0.05f;
            if (styleAnimTimer > MathHelper.TwoPi) {
                styleAnimTimer -= MathHelper.TwoPi;
            }
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(3, 3);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * 0.5f * alpha);

            //绘制背景
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

            //绘制边框
            Color edgeColor = GetEdgeColor(alpha);
            DrawBorder(spriteBatch, panelRect, edgeColor);

            //绘制装饰星星
            float starTime = Main.GlobalTimeWrappedHourly * 3f;
            Vector2 star1 = new Vector2(panelRect.Right - 18, panelRect.Y + 14);
            float s1a = ((float)Math.Sin(starTime) * 0.5f + 0.5f) * alpha;
            DrawStar(spriteBatch, star1, 3.5f, edgeColor * s1a);
        }

        public void DrawChoiceBackground(SpriteBatch spriteBatch, Rectangle choiceRect, bool enabled, float hoverProgress, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //选项背景
            Color choiceBg = enabled
                ? Color.Lerp(new Color(20, 35, 50) * 0.3f, new Color(40, 70, 100) * 0.5f, hoverProgress)
                : new Color(15, 15, 20) * 0.15f;

            spriteBatch.Draw(pixel, choiceRect, new Rectangle(0, 0, 1, 1), choiceBg * alpha);

            //选项边框
            Color edgeColor = GetEdgeColor(alpha);
            if (enabled && hoverProgress > 0.01f) {
                DrawChoiceBorder(spriteBatch, choiceRect, edgeColor * (hoverProgress * 0.6f));
            }
            else if (!enabled) {
                DrawChoiceBorder(spriteBatch, choiceRect, new Color(60, 60, 70) * (alpha * 0.25f));
            }
        }

        public Color GetEdgeColor(float alpha) {
            return new Color(70, 180, 230) * (alpha * 0.75f);
        }

        public Color GetTextGlowColor(float alpha, float hoverProgress) {
            return GetEdgeColor(alpha);
        }

        public void DrawTitleDecoration(SpriteBatch spriteBatch, Vector2 titlePos, string title, float alpha) {
            Color edgeColor = GetEdgeColor(alpha);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f;
                Vector2 o = ang.ToRotationVector2() * 1.25f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + o, edgeColor * 0.55f, 0.9f);
            }
        }

        public void DrawDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha) {
            Color edgeColor = GetEdgeColor(alpha);
            DrawGradientLine(spriteBatch, start, end, edgeColor * 0.9f, edgeColor * 0.05f, 1.3f);
        }

        public void Reset() {
            styleAnimTimer = 0f;
        }

        #region 工具方法
        private static void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2),
                new Rectangle(0, 0, 1, 1), color * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height),
                new Rectangle(0, 0, 1, 1), color * 0.85f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height),
                new Rectangle(0, 0, 1, 1), color * 0.85f);

            DrawCornerStar(spriteBatch, new Vector2(rect.X + 8, rect.Y + 8), color * 0.9f);
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 8, rect.Y + 8), color * 0.9f);
        }

        private static void DrawChoiceBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height),
                new Rectangle(0, 0, 1, 1), color);
        }

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end,
            Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) return;

            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 10f));

            for (int i = 0; i < segments; i++) {
                float t = (float)i / segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation,
                    new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }

        private static void DrawStar(SpriteBatch spriteBatch, Vector2 pos, float size, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * 0.8f, MathHelper.PiOver2,
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
        }

        private static void DrawCornerStar(SpriteBatch spriteBatch, Vector2 pos, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float size = 4f;
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.3f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * 0.7f, MathHelper.PiOver2,
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.3f), SpriteEffects.None, 0f);
        }
        #endregion
    }
}
