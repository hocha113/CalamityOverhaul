using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.ADVChoiceBoxs.Styles
{
    /// <summary>
    /// 嘉登科技风格
    /// </summary>
    internal class DraedonChoiceBoxStyle : IChoiceBoxStyle
    {
        private float draedonScanLineTimer = 0f;
        private float draedonHologramFlicker = 0f;
        private readonly List<DraedonDataStream> draedonStreams = new();
        private int draedonStreamTimer = 0;

        public void Update(Rectangle panelRect, bool active, bool closing) {
            draedonScanLineTimer += 0.048f;
            draedonHologramFlicker += 0.12f;
            if (draedonScanLineTimer > MathHelper.TwoPi) draedonScanLineTimer -= MathHelper.TwoPi;
            if (draedonHologramFlicker > MathHelper.TwoPi) draedonHologramFlicker -= MathHelper.TwoPi;

            //生成数据流
            draedonStreamTimer++;
            if (active && !closing && draedonStreamTimer >= 15 && draedonStreams.Count < 12) {
                draedonStreamTimer = 0;
                float xPos = Main.rand.NextFloat(panelRect.X + 20f, panelRect.Right - 20f);
                Vector2 startPos = new(xPos, panelRect.Y + Main.rand.NextFloat(20f, panelRect.Height - 20f));
                draedonStreams.Add(new DraedonDataStream(startPos));
            }

            //更新数据流
            for (int i = draedonStreams.Count - 1; i >= 0; i--) {
                if (draedonStreams[i].Update(panelRect)) {
                    draedonStreams.RemoveAt(i);
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(5, 6);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.65f));

            //科技背景渐变
            int segments = 25;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                float pulse = (float)Math.Sin(draedonHologramFlicker * 0.6f + t * 2.0f) * 0.5f + 0.5f;
                Color techDark = new Color(8, 12, 22);
                Color techMid = new Color(18, 28, 42);
                Color techEdge = new Color(35, 55, 85);

                Color blendBase = Color.Lerp(techDark, techMid, pulse);
                Color c = Color.Lerp(blendBase, techEdge, t * 0.45f);
                c *= alpha * 0.92f;

                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), c);
            }

            //全息闪烁覆盖层
            float flicker = (float)Math.Sin(draedonHologramFlicker * 1.5f) * 0.5f + 0.5f;
            Color hologramOverlay = new Color(15, 30, 45) * (alpha * 0.25f * flicker);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), hologramOverlay);

            //绘制六角网格纹理
            DrawDraedonHexGrid(spriteBatch, panelRect, alpha * 0.75f);

            //绘制扫描线
            DrawDraedonScanLines(spriteBatch, panelRect, alpha * 0.85f);

            //科技边框
            Color techEdgeColor = GetEdgeColor(alpha);
            DrawBorder(spriteBatch, panelRect, techEdgeColor);

            //绘制数据流粒子
            foreach (var stream in draedonStreams) {
                stream.Draw(spriteBatch, alpha * 0.85f);
            }
        }

        public void DrawChoiceBackground(SpriteBatch spriteBatch, Rectangle choiceRect, bool enabled, float hoverProgress, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Color choiceBg = enabled
                ? Color.Lerp(new Color(8, 16, 30) * 0.3f, new Color(20, 40, 65) * 0.5f, hoverProgress)
                : new Color(10, 12, 15) * 0.15f;

            spriteBatch.Draw(pixel, choiceRect, new Rectangle(0, 0, 1, 1), choiceBg * alpha);

            Color techColor = GetEdgeColor(alpha);
            if (enabled && hoverProgress > 0.01f) {
                DrawChoiceBorder(spriteBatch, choiceRect, techColor * (hoverProgress * 0.6f));

                //绘制数据流效果
                float dataShift = (float)Math.Sin(draedonHologramFlicker * 3f) * 1.5f;
                Color dataColor = techColor * (hoverProgress * 0.2f);
                spriteBatch.Draw(pixel,
                    new Rectangle((int)(choiceRect.X + dataShift), choiceRect.Y, 1, choiceRect.Height),
                    dataColor);
            }
            else if (!enabled) {
                DrawChoiceBorder(spriteBatch, choiceRect, new Color(40, 60, 80) * (alpha * 0.2f));
            }
        }

        public Color GetEdgeColor(float alpha) {
            float flicker = (float)Math.Sin(draedonHologramFlicker * 1.5f) * 0.5f + 0.5f;
            return Color.Lerp(new Color(40, 160, 240), new Color(80, 200, 255), flicker) * (alpha * 0.85f);
        }

        public Color GetTextGlowColor(float alpha, float hoverProgress) {
            return GetEdgeColor(alpha);
        }

        public void DrawTitleDecoration(SpriteBatch spriteBatch, Vector2 titlePos, string title, float alpha) {
            Color nameGlow = new Color(80, 220, 255) * alpha * 0.8f;
            for (int i = 0; i < 4; i++) {
                float a = MathHelper.TwoPi * i / 4f;
                Vector2 off = a.ToRotationVector2() * 2f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + off, nameGlow * 0.6f, 0.95f);
            }
        }

        public void DrawDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha) {
            DrawGradientLine(spriteBatch, start, end,
                new Color(60, 160, 240) * (alpha * 0.9f),
                new Color(60, 160, 240) * (alpha * 0.08f), 1.5f);
        }

        public void Reset() {
            draedonScanLineTimer = 0f;
            draedonHologramFlicker = 0f;
            draedonStreams.Clear();
            draedonStreamTimer = 0;
        }

        #region 工具方法
        private void DrawDraedonHexGrid(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int hexRows = 6;
            float hexHeight = rect.Height / (float)hexRows;

            for (int row = 0; row < hexRows; row++) {
                float t = row / (float)hexRows;
                float y = rect.Y + row * hexHeight;
                float phase = draedonScanLineTimer + t * MathHelper.Pi;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                Color gridColor = new Color(25, 90, 140) * (alpha * 0.04f * brightness);
                sb.Draw(pixel, new Rectangle(rect.X + 8, (int)y, rect.Width - 16, 1), new Rectangle(0, 0, 1, 1), gridColor);
            }
        }

        private void DrawDraedonScanLines(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float scanY = rect.Y + (float)Math.Sin(draedonScanLineTimer) * 0.5f * rect.Height + rect.Height * 0.5f;

            for (int i = -2; i <= 2; i++) {
                float offsetY = scanY + i * 3f;
                if (offsetY < rect.Y || offsetY > rect.Bottom) {
                    continue;
                }

                float intensity = 1f - Math.Abs(i) * 0.3f;
                Color scanColor = new Color(60, 180, 255) * (alpha * 0.15f * intensity);
                sb.Draw(pixel, new Rectangle(rect.X + 6, (int)offsetY, rect.Width - 12, 2), new Rectangle(0, 0, 1, 1), scanColor);
            }
        }

        private static void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3),
                new Rectangle(0, 0, 1, 1), color * 0.75f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height),
                new Rectangle(0, 0, 1, 1), color * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height),
                new Rectangle(0, 0, 1, 1), color * 0.9f);

            DrawCornerCircuit(spriteBatch, new Vector2(rect.X + 12, rect.Y + 12), color * 0.95f);
            DrawCornerCircuit(spriteBatch, new Vector2(rect.Right - 12, rect.Y + 12), color * 0.95f);
        }

        private static void DrawCornerCircuit(SpriteBatch sb, Vector2 pos, Color c) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 6f;

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.85f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.6f, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 0.4f, size * 0.4f), SpriteEffects.None, 0f);
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
        #endregion

        #region 粒子类
        private class DraedonDataStream
        {
            public Vector2 Pos;
            public float Size;
            public float Life;
            public float MaxLife;
            public float Seed;
            public Vector2 Velocity;
            public float Rotation;

            public DraedonDataStream(Vector2 start) {
                Pos = start;
                Size = Main.rand.NextFloat(1.5f, 3f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(70f, 120f);
                Seed = Main.rand.NextFloat(10f);
                Velocity = new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-0.6f, -0.2f));
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            public bool Update(Rectangle bounds) {
                Life++;
                Rotation += 0.025f;
                Pos += Velocity;
                Velocity.Y -= 0.015f;

                if (Life >= MaxLife || Pos.X < bounds.X - 30 || Pos.X > bounds.Right + 30 ||
                    Pos.Y < bounds.Y - 30 || Pos.Y > bounds.Bottom + 30) {
                    return true;
                }
                return false;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi) * alpha;
                float scale = Size * (0.7f + (float)Math.Sin((Life + Seed * 40f) * 0.09f) * 0.3f);

                Color c = new Color(80, 200, 255) * (0.8f * fade);
                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), c, Rotation, new Vector2(0.5f, 0.5f), new Vector2(scale * 2f, scale * 0.3f), SpriteEffects.None, 0f);
                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), c * 0.9f, Rotation + MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(scale * 2f, scale * 0.3f), SpriteEffects.None, 0f);
            }
        }
        #endregion
    }
}
