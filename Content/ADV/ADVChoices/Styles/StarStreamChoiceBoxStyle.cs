using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.ADVChoices.Styles
{
    /// <summary>星流风格选择框</summary>
    internal class StarStreamChoiceBoxStyle : IChoiceBoxStyle
    {
        private float starFlowTimer = 0f;
        private float nebulaPulseTimer = 0f;
        private float shimmerTimer = 0f;
        private float constellationPhase = 0f;
        private readonly List<StarStreamDataStream> streams = new();
        private int streamTimer = 0;

        public void Update(Rectangle panelRect, bool active, bool closing) {
            starFlowTimer += 0.04f;
            nebulaPulseTimer += 0.022f;
            shimmerTimer += 0.035f;
            constellationPhase += 0.012f;
            if (starFlowTimer > MathHelper.TwoPi) starFlowTimer -= MathHelper.TwoPi;
            if (nebulaPulseTimer > MathHelper.TwoPi) nebulaPulseTimer -= MathHelper.TwoPi;
            if (shimmerTimer > MathHelper.TwoPi) shimmerTimer -= MathHelper.TwoPi;
            if (constellationPhase > MathHelper.TwoPi) constellationPhase -= MathHelper.TwoPi;

            //生成数据流
            streamTimer++;
            if (active && !closing && streamTimer >= 16 && streams.Count < 12) {
                streamTimer = 0;
                float xPos = Main.rand.NextFloat(panelRect.X + 20f, panelRect.Right - 20f);
                Vector2 startPos = new(xPos, panelRect.Y + Main.rand.NextFloat(20f, panelRect.Height - 20f));
                streams.Add(new StarStreamDataStream(startPos));
            }

            //更新数据流
            for (int i = streams.Count - 1; i >= 0; i--) {
                if (streams[i].Update(panelRect)) {
                    streams.RemoveAt(i);
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle panelRect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //绘制阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(5, 7);
            spriteBatch.Draw(px, shadowRect, new Rectangle(0, 0, 1, 1), new Color(5, 0, 15) * (alpha * 0.6f));

            //深空背景渐变
            int segments = 25;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                float nebula = (float)Math.Sin(nebulaPulseTimer * 0.5f + t * 1.8f) * 0.5f + 0.5f;
                Color deepSpace = new Color(6, 4, 16);
                Color midSpace = new Color(12, 10, 28);
                Color edgeSpace = new Color(22, 18, 45);

                Color blendBase = Color.Lerp(deepSpace, midSpace, nebula);
                Color c = Color.Lerp(blendBase, edgeSpace, t * 0.5f);
                c *= alpha * 0.94f;

                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), c);
            }

            //星云呼吸叠加
            float nebulaPulse = (float)Math.Sin(nebulaPulseTimer * 1.3f) * 0.5f + 0.5f;
            Color nebulaOverlay = new Color(30, 15, 50) * (alpha * 0.2f * nebulaPulse);
            spriteBatch.Draw(px, panelRect, new Rectangle(0, 0, 1, 1), nebulaOverlay);

            //星座网格
            DrawConstellationGrid(spriteBatch, panelRect, alpha * 0.6f);

            //金色边框
            Color edgeColor = GetEdgeColor(alpha);
            DrawBorder(spriteBatch, panelRect, edgeColor);

            //绘制星流粒子
            foreach (var stream in streams) {
                stream.Draw(spriteBatch, alpha * 0.8f);
            }
        }

        public void DrawChoiceBackground(SpriteBatch spriteBatch, Rectangle choiceRect, bool enabled, float hoverProgress, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            Color choiceBg = enabled
                ? Color.Lerp(new Color(12, 8, 25) * 0.3f, new Color(30, 22, 50) * 0.5f, hoverProgress)
                : new Color(10, 8, 15) * 0.15f;

            spriteBatch.Draw(px, choiceRect, new Rectangle(0, 0, 1, 1), choiceBg * alpha);

            Color goldColor = GetEdgeColor(alpha);
            if (enabled && hoverProgress > 0.01f) {
                DrawChoiceBorder(spriteBatch, choiceRect, goldColor * (hoverProgress * 0.6f));

                //金色流光效果
                float shimmer = (float)Math.Sin(shimmerTimer * 3f) * 1.5f;
                Color shimmerColor = goldColor * (hoverProgress * 0.2f);
                spriteBatch.Draw(px,
                    new Rectangle((int)(choiceRect.X + shimmer), choiceRect.Y, 1, choiceRect.Height),
                    shimmerColor);
            }
            else if (!enabled) {
                DrawChoiceBorder(spriteBatch, choiceRect, new Color(50, 40, 30) * (alpha * 0.2f));
            }
        }

        public Color GetEdgeColor(float alpha) {
            float pulse = (float)Math.Sin(shimmerTimer * 1.1f) * 0.5f + 0.5f;
            return Color.Lerp(new Color(180, 140, 50), new Color(240, 200, 100), pulse) * (alpha * 0.8f);
        }

        public Color GetTextGlowColor(float alpha, float hoverProgress) {
            return GetEdgeColor(alpha);
        }

        public void DrawTitleDecoration(SpriteBatch spriteBatch, Vector2 titlePos, string title, float alpha) {
            Color nameGlow = new Color(255, 210, 120) * alpha * 0.7f;
            for (int i = 0; i < 5; i++) {
                float a = MathHelper.TwoPi * i / 5f + shimmerTimer * 0.3f;
                Vector2 off = a.ToRotationVector2() * 2.2f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + off, nameGlow * 0.5f, 0.95f);
            }
        }

        public void DrawDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha) {
            DrawGradientLine(spriteBatch, start, end,
                new Color(220, 180, 80) * (alpha * 0.85f),
                new Color(220, 180, 80) * (alpha * 0.06f), 1.5f);
        }

        public void Reset() {
            starFlowTimer = 0f;
            nebulaPulseTimer = 0f;
            shimmerTimer = 0f;
            constellationPhase = 0f;
            streams.Clear();
            streamTimer = 0;
        }

        #region 工具方法

        private void DrawConstellationGrid(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int rows = 5;
            float rowHeight = rect.Height / (float)rows;

            for (int row = 0; row < rows; row++) {
                float t = row / (float)rows;
                float y = rect.Y + row * rowHeight;
                float phase = constellationPhase + t * MathHelper.Pi;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                Color gridColor = new Color(180, 150, 80) * (alpha * 0.03f * brightness);
                sb.Draw(px, new Rectangle(rect.X + 8, (int)y, rect.Width - 16, 1), new Rectangle(0, 0, 1, 1), gridColor);
            }
        }

        private void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D px = VaultAsset.placeholder2.Value;

            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), color * 0.7f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), color * 0.9f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), color * 0.9f);

            //顶部流光
            float flowT = (shimmerTimer * 0.8f) % 1f;
            int highlightW = 60;
            int highlightX = rect.X + (int)(flowT * (rect.Width - highlightW));
            Color highlightColor = new Color(255, 230, 140) * (color.A / 255f * 0.3f);
            for (int dx = 0; dx < highlightW; dx++) {
                float localT = dx / (float)highlightW;
                float intensity = (float)Math.Sin(localT * MathHelper.Pi);
                spriteBatch.Draw(px, new Rectangle(highlightX + dx, rect.Y, 1, 3), new Rectangle(0, 0, 1, 1), highlightColor * intensity);
            }

            DrawCornerStar(spriteBatch, new Vector2(rect.X + 12, rect.Y + 12), color * 0.95f);
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 12, rect.Y + 12), color * 0.95f);
        }

        private static void DrawCornerStar(SpriteBatch sb, Vector2 pos, Color c) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 6f;

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 1.3f, size * 0.22f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.85f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size * 1.3f, size * 0.22f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.5f, MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.8f, size * 0.18f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.5f, -MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.8f, size * 0.18f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.7f, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 0.35f, size * 0.35f), SpriteEffects.None, 0f);
        }

        private static void DrawChoiceBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D px = VaultAsset.placeholder2.Value;

            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), color);
        }

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end,
            Color startColor, Color endColor, float thickness) {
            Texture2D px = VaultAsset.placeholder2.Value;
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
                spriteBatch.Draw(px, segPos, new Rectangle(0, 0, 1, 1), color, rotation,
                    new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }

        #endregion

        #region 粒子类
        private class StarStreamDataStream
        {
            public Vector2 Pos;
            public float Size;
            public float Life;
            public float MaxLife;
            public float Seed;
            public Vector2 Velocity;
            public float Rotation;

            public StarStreamDataStream(Vector2 start) {
                Pos = start;
                Size = Main.rand.NextFloat(1.5f, 3.5f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(80f, 140f);
                Seed = Main.rand.NextFloat(10f);
                Velocity = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.5f, -0.15f));
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            public bool Update(Rectangle bounds) {
                Life++;
                Rotation += 0.018f;
                Pos += Velocity;
                Velocity.Y -= 0.008f;
                Velocity.X += (float)Math.Sin(Life * 0.05f + Seed) * 0.02f;

                if (Life >= MaxLife || Pos.X < bounds.X - 30 || Pos.X > bounds.Right + 30 ||
                    Pos.Y < bounds.Y - 30 || Pos.Y > bounds.Bottom + 30) {
                    return true;
                }
                return false;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D px = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi) * alpha;
                float scale = Size * (0.6f + (float)Math.Sin((Life + Seed * 30f) * 0.07f) * 0.4f);

                Color gold = new Color(255, 210, 100) * (0.85f * fade);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), gold, Rotation, new Vector2(0.5f, 0.5f), new Vector2(scale * 2.5f, scale * 0.35f), SpriteEffects.None, 0f);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), gold * 0.8f, Rotation + MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(scale * 2.5f, scale * 0.35f), SpriteEffects.None, 0f);

                Color warm = new Color(255, 240, 200) * (0.4f * fade);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), warm, 0f, new Vector2(0.5f), new Vector2(scale * 0.5f), SpriteEffects.None, 0f);
            }
        }
        #endregion
    }
}
