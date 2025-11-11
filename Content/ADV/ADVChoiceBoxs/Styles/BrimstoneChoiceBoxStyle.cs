using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.ADVChoiceBoxs.Styles
{
    /// <summary>
    /// 硫磺火风格
    /// </summary>
    internal class BrimstoneChoiceBoxStyle : IChoiceBoxStyle
    {
        private float brimstoneFlameTimer = 0f;
        private readonly List<BrimstoneEmber> brimstoneEmbers = new();
        private int brimstoneEmberTimer = 0;

        public void Update(Rectangle panelRect, bool active, bool closing) {
            brimstoneFlameTimer += 0.045f;
            if (brimstoneFlameTimer > MathHelper.TwoPi) {
                brimstoneFlameTimer -= MathHelper.TwoPi;
            }

            //生成余烬粒子
            brimstoneEmberTimer++;
            if (active && !closing && brimstoneEmberTimer >= 6 && brimstoneEmbers.Count < 20) {
                brimstoneEmberTimer = 0;
                float xPos = Main.rand.NextFloat(panelRect.X + 15f, panelRect.Right - 15f);
                Vector2 startPos = new(xPos, panelRect.Bottom - 5f);
                brimstoneEmbers.Add(new BrimstoneEmber(startPos));
            }

            //更新粒子
            for (int i = brimstoneEmbers.Count - 1; i >= 0; i--) {
                if (brimstoneEmbers[i].Update(panelRect)) {
                    brimstoneEmbers.RemoveAt(i);
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(7, 9);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), new Color(20, 0, 0) * (alpha * 0.65f));

            //渐变背景 - 硫磺火深红色
            int segments = 25;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                float flameWave = (float)Math.Sin(brimstoneFlameTimer * 0.6f + t * 2.2f) * 0.5f + 0.5f;
                Color brimstoneDeep = new Color(25, 5, 5);
                Color brimstoneMid = new Color(80, 15, 10);
                Color brimstoneHot = new Color(140, 35, 20);

                Color baseColor = Color.Lerp(brimstoneDeep, brimstoneMid, flameWave);
                Color finalColor = Color.Lerp(baseColor, brimstoneHot, t * 0.5f);
                finalColor *= alpha * 0.92f;

                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), finalColor);
            }

            //火焰脉冲叠加
            float pulseBrightness = (float)Math.Sin(brimstoneFlameTimer * 1.8f) * 0.5f + 0.5f;
            Color pulseOverlay = new Color(120, 25, 15) * (alpha * 0.25f * pulseBrightness);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), pulseOverlay);

            //绘制热浪扭曲效果
            DrawBrimstoneHeatWaves(spriteBatch, panelRect, alpha * 0.75f);

            //火焰边框
            Color flameEdge = GetEdgeColor(alpha);
            DrawBorder(spriteBatch, panelRect, flameEdge);

            //绘制余烬粒子
            foreach (var ember in brimstoneEmbers) {
                ember.Draw(spriteBatch, alpha * 0.9f);
            }
        }

        public void DrawChoiceBackground(SpriteBatch spriteBatch, Rectangle choiceRect, bool enabled, float hoverProgress, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Color choiceBg = enabled
                ? Color.Lerp(new Color(40, 10, 5) * 0.3f, new Color(100, 25, 15) * 0.5f, hoverProgress)
                : new Color(20, 10, 8) * 0.12f;

            spriteBatch.Draw(pixel, choiceRect, new Rectangle(0, 0, 1, 1), choiceBg * alpha);

            Color flameColor = GetEdgeColor(alpha);
            if (enabled && hoverProgress > 0.01f) {
                DrawChoiceBorder(spriteBatch, choiceRect, flameColor * (hoverProgress * 0.6f));
            }
            else if (!enabled) {
                DrawChoiceBorder(spriteBatch, choiceRect, new Color(80, 40, 30) * (alpha * 0.2f));
            }
        }

        public Color GetEdgeColor(float alpha) {
            float pulseBrightness = (float)Math.Sin(brimstoneFlameTimer * 1.8f) * 0.5f + 0.5f;
            return Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), pulseBrightness) * (alpha * 0.85f);
        }

        public Color GetTextGlowColor(float alpha, float hoverProgress) {
            return GetEdgeColor(alpha);
        }

        public void DrawTitleDecoration(SpriteBatch spriteBatch, Vector2 titlePos, string title, float alpha) {
            Color flameEdge = GetEdgeColor(alpha);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f;
                Vector2 o = ang.ToRotationVector2() * 1.25f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + o, flameEdge * 0.55f, 0.9f);
            }
        }

        public void DrawDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha) {
            Color flameEdge = GetEdgeColor(alpha);
            DrawGradientLine(spriteBatch, start, end, flameEdge * 0.9f, flameEdge * 0.05f, 1.3f);
        }

        public void Reset() {
            brimstoneFlameTimer = 0f;
            brimstoneEmbers.Clear();
            brimstoneEmberTimer = 0;
        }

        #region 工具方法
        private void DrawBrimstoneHeatWaves(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int waveCount = 5;
            for (int i = 0; i < waveCount; i++) {
                float t = i / (float)waveCount;
                float baseY = rect.Y + 15 + t * (rect.Height - 30);
                float amplitude = 3f + (float)Math.Sin((brimstoneFlameTimer + t * 1.2f) * 2.5f) * 2f;

                int segments = 30;
                Vector2 prevPoint = Vector2.Zero;
                for (int s = 0; s <= segments; s++) {
                    float progress = s / (float)segments;
                    float waveY = baseY + (float)Math.Sin(brimstoneFlameTimer * 3f + progress * MathHelper.TwoPi * 1.5f + t * 2f) * amplitude;
                    Vector2 point = new(rect.X + 10 + progress * (rect.Width - 20), waveY);

                    if (s > 0) {
                        Vector2 diff = point - prevPoint;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            Color waveColor = new Color(180, 60, 30) * (alpha * 0.08f);
                            sb.Draw(pixel, prevPoint, new Rectangle(0, 0, 1, 1), waveColor, rot, Vector2.Zero, new Vector2(len, 1.2f), SpriteEffects.None, 0f);
                        }
                    }
                    prevPoint = point;
                }
            }
        }

        private static void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3),
                new Rectangle(0, 0, 1, 1), color * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height),
                new Rectangle(0, 0, 1, 1), color * 0.85f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height),
                new Rectangle(0, 0, 1, 1), color * 0.85f);
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
        private class BrimstoneEmber
        {
            public Vector2 Pos;
            public float Size;
            public float RiseSpeed;
            public float Drift;
            public float Life;
            public float MaxLife;
            public float Seed;
            public float Rotation;
            public float RotationSpeed;

            public BrimstoneEmber(Vector2 start) {
                Pos = start;
                Size = Main.rand.NextFloat(2f, 4.5f);
                RiseSpeed = Main.rand.NextFloat(0.5f, 1.2f);
                Drift = Main.rand.NextFloat(-0.3f, 0.3f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(60f, 110f);
                Seed = Main.rand.NextFloat(10f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                RotationSpeed = Main.rand.NextFloat(-0.06f, 0.06f);
            }

            public bool Update(Rectangle bounds) {
                Life++;
                float t = Life / MaxLife;
                Pos.Y -= RiseSpeed * (1f - t * 0.3f);
                Pos.X += (float)Math.Sin(Life * 0.06f + Seed) * Drift;
                Rotation += RotationSpeed;

                if (Life >= MaxLife || Pos.Y < bounds.Y - 10f) {
                    return true;
                }
                return false;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI);
                float scale = Size * (1f + (float)Math.Sin((Life + Seed * 20f) * 0.12f) * 0.15f);

                Color emberCore = Color.Lerp(new Color(255, 180, 80), new Color(255, 80, 40), t) * (alpha * 0.85f * fade);
                Color emberGlow = Color.Lerp(new Color(255, 140, 60), new Color(180, 40, 20), t) * (alpha * 0.5f * fade);

                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), emberGlow, 0f, new Vector2(0.5f, 0.5f), scale * 2.2f, SpriteEffects.None, 0f);
                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), emberCore, Rotation, new Vector2(0.5f, 0.5f), scale, SpriteEffects.None, 0f);
            }
        }
        #endregion
    }
}
