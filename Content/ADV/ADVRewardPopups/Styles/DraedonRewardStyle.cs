using CalamityOverhaul.Content.ADV.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.ADVRewardPopups.Styles
{
    /// <summary>
    /// 嘉登科技风格奖励弹窗
    /// </summary>
    internal class DraedonRewardStyle : IRewardPopupStyle
    {
        private float draedonScanLineTimer = 0f;
        private float draedonHologramFlicker = 0f;
        private float draedonCircuitPulse = 0f;
        private float draedonDataStream = 0f;
        private float draedonHexGridPhase = 0f;
        private readonly List<DraedonDataPRT> draedonDataParticles = new();
        private int draedonDataParticleTimer = 0;
        private readonly List<CircuitNodePRT> draedonCircuitNodes = new();
        private int draedonCircuitNodeTimer = 0;
        private const float DraedonParticleMargin = 30f;

        public void Update(Rectangle panelRect, bool active, bool closing) {
            draedonScanLineTimer += 0.048f;
            draedonHologramFlicker += 0.12f;
            draedonCircuitPulse += 0.025f;
            draedonDataStream += 0.055f;
            draedonHexGridPhase += 0.015f;
            if (draedonScanLineTimer > MathHelper.TwoPi) draedonScanLineTimer -= MathHelper.TwoPi;
            if (draedonHologramFlicker > MathHelper.TwoPi) draedonHologramFlicker -= MathHelper.TwoPi;
            if (draedonCircuitPulse > MathHelper.TwoPi) draedonCircuitPulse -= MathHelper.TwoPi;
            if (draedonDataStream > MathHelper.TwoPi) draedonDataStream -= MathHelper.TwoPi;
            if (draedonHexGridPhase > MathHelper.TwoPi) draedonHexGridPhase -= MathHelper.TwoPi;
        }

        public void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //主背景渐变 - 深蓝科技色调
            int segments = 30;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Rectangle r = new(rect.X, y1, rect.Width, Math.Max(1, y2 - y1));

                Color techDark = new Color(8, 12, 22);
                Color techMid = new Color(18, 28, 42);
                Color techBright = new Color(35, 55, 85);

                float pulse = (float)Math.Sin(draedonCircuitPulse * 0.6f + t * 2.0f) * 0.5f + 0.5f;
                Color baseColor = Color.Lerp(techDark, techMid, pulse);
                Color finalColor = Color.Lerp(baseColor, techBright, t * 0.45f);
                finalColor *= alpha * (0.92f + hoverGlow);

                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), finalColor);
            }

            //全息闪烁覆盖层
            float flicker = (float)Math.Sin(draedonHologramFlicker * 1.5f) * 0.5f + 0.5f;
            Color hologramOverlay = new Color(15, 30, 45) * (alpha * 0.25f * flicker);
            spriteBatch.Draw(px, rect, new Rectangle(0, 0, 1, 1), hologramOverlay);

            //六角网格纹理
            DrawHexGrid(spriteBatch, rect, alpha * 0.85f);

            //扫描线效果
            DrawScanLines(spriteBatch, rect, alpha * 0.9f);

            //电路脉冲内发光
            float innerPulse = (float)Math.Sin(draedonCircuitPulse * 1.3f) * 0.5f + 0.5f;
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            spriteBatch.Draw(px, inner, new Rectangle(0, 0, 1, 1), new Color(40, 180, 255) * (alpha * (0.12f + hoverGlow * 0.5f) * innerPulse));
        }

        public void DrawFrame(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float pulse = (float)Math.Sin(draedonCircuitPulse * 1.3f) * 0.5f + 0.5f;

            Color techEdge = Color.Lerp(new Color(40, 160, 240), new Color(80, 200, 255), pulse) * (alpha * (0.85f + hoverGlow * 0.3f));
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), techEdge);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), techEdge * 0.75f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), techEdge * 0.9f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), techEdge * 0.9f);

            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(100, 200, 255) * (alpha * (0.22f + hoverGlow * 0.5f) * pulse);
            spriteBatch.Draw(px, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            spriteBatch.Draw(px, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            spriteBatch.Draw(px, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.9f);
            spriteBatch.Draw(px, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.9f);

            DrawCircuitMark(spriteBatch, new Vector2(rect.X + 12, rect.Y + 12), alpha * (0.95f + hoverGlow * 0.4f));
            DrawCircuitMark(spriteBatch, new Vector2(rect.Right - 12, rect.Y + 12), alpha * (0.95f + hoverGlow * 0.4f));
            DrawCircuitMark(spriteBatch, new Vector2(rect.X + 12, rect.Bottom - 12), alpha * (0.65f + hoverGlow * 0.3f));
            DrawCircuitMark(spriteBatch, new Vector2(rect.Right - 12, rect.Bottom - 12), alpha * (0.65f + hoverGlow * 0.3f));
        }

        public Color GetNameGlowColor(float alpha) {
            return new Color(80, 220, 255) * (alpha * 0.8f);
        }

        public Color GetNameColor(float alpha) {
            return Color.White * alpha;
        }

        public Color GetHintColor(float alpha, float blink) {
            return new Color(80, 220, 255) * (alpha * blink);
        }

        public void Reset() {
            draedonScanLineTimer = 0f;
            draedonHologramFlicker = 0f;
            draedonCircuitPulse = 0f;
            draedonDataStream = 0f;
            draedonHexGridPhase = 0f;
            draedonDataParticles.Clear();
            draedonCircuitNodes.Clear();
            draedonDataParticleTimer = 0;
            draedonCircuitNodeTimer = 0;
        }

        public void GetParticles(out List<object> particles) {
            particles = [.. draedonCircuitNodes, .. draedonDataParticles];
        }

        public void UpdateParticles(Vector2 basePos, float panelFade) {
            draedonDataParticleTimer++;
            if (panelFade > 0.6f && draedonDataParticleTimer >= 15 && draedonDataParticles.Count < 18) {
                draedonDataParticleTimer = 0;
                float xPos = Main.rand.NextFloat(basePos.X - 100f + DraedonParticleMargin, basePos.X + 100f - DraedonParticleMargin);
                Vector2 startPos = new(xPos, basePos.Y + Main.rand.NextFloat(-40f, 40f));
                draedonDataParticles.Add(new DraedonDataPRT(startPos));
            }
            for (int i = draedonDataParticles.Count - 1; i >= 0; i--) {
                if (draedonDataParticles[i].Update(basePos)) {
                    draedonDataParticles.RemoveAt(i);
                }
            }

            draedonCircuitNodeTimer++;
            if (panelFade > 0.6f && draedonCircuitNodeTimer >= 30 && draedonCircuitNodes.Count < 10) {
                draedonCircuitNodeTimer = 0;
                Vector2 startPos = new(
                    Main.rand.NextFloat(basePos.X - 90f, basePos.X + 90f),
                    Main.rand.NextFloat(basePos.Y - 50f, basePos.Y + 50f)
                );
                draedonCircuitNodes.Add(new CircuitNodePRT(startPos));
            }
            for (int i = draedonCircuitNodes.Count - 1; i >= 0; i--) {
                if (draedonCircuitNodes[i].Update()) {
                    draedonCircuitNodes.RemoveAt(i);
                }
            }
        }

        private void DrawHexGrid(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int hexRows = 6;
            float hexHeight = rect.Height / (float)hexRows;

            for (int row = 0; row < hexRows; row++) {
                float t = row / (float)hexRows;
                float y = rect.Y + row * hexHeight;
                float phase = draedonHexGridPhase + t * MathHelper.Pi;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                Color gridColor = new Color(25, 90, 140) * (alpha * 0.04f * brightness);
                sb.Draw(px, new Rectangle(rect.X + 10, (int)y, rect.Width - 20, 1), new Rectangle(0, 0, 1, 1), gridColor);
            }
        }

        private void DrawScanLines(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float scanY = rect.Y + (float)Math.Sin(draedonScanLineTimer) * 0.5f * rect.Height + rect.Height * 0.5f;

            for (int i = -2; i <= 2; i++) {
                float offsetY = scanY + i * 3f;
                if (offsetY < rect.Y || offsetY > rect.Bottom) {
                    continue;
                }

                float intensity = 1f - Math.Abs(i) * 0.3f;
                Color scanColor = new Color(60, 180, 255) * (alpha * 0.15f * intensity);
                sb.Draw(px, new Rectangle(rect.X + 8, (int)offsetY, rect.Width - 16, 2), new Rectangle(0, 0, 1, 1), scanColor);
            }
        }

        private static void DrawCircuitMark(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 6f;
            Color techColor = new Color(100, 220, 255) * alpha;

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), techColor, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), techColor * 0.85f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), techColor * 0.6f, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 0.4f, size * 0.4f), SpriteEffects.None, 0f);
        }
    }
}
