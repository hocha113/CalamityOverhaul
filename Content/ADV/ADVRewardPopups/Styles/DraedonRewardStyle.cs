using CalamityOverhaul.Content.ADV.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.ADV.ADVRewardPopups.Styles
{
    /// <summary>
    /// 嘉登科技奖励弹窗
    /// </summary>
    internal class DraedonRewardStyle : IRewardPopupStyle
    {
        // 动画计时器
        private float circuitPulseTimer;
        private float hologramFlicker;
        private float dataStreamTimer;
        private float sweepTimer;
        private float glitchTimer;

        // 四角十六进制读出
        private readonly string[] cornerHex = ["0x????", "0x????", "0x????", "0x????"];
        private int hexUpdateClock;
        private static readonly char[] HexChars = "0123456789ABCDEF".ToCharArray();

        // 粒子
        private readonly List<DraedonDataPRT> dataParticles = [];
        private int dataParticleTimer;
        private readonly List<CircuitNodePRT> circuitNodes = [];
        private int circuitNodeTimer;

        public void Update(Rectangle panelRect, bool active, bool closing) {
            Advance(ref circuitPulseTimer, 0.025f);
            Advance(ref hologramFlicker, 0.13f);
            Advance(ref dataStreamTimer, 0.022f);
            sweepTimer = (sweepTimer + 0.008f) % 1f;
            glitchTimer += 0.16f;
            if (glitchTimer > MathHelper.TwoPi) glitchTimer -= MathHelper.TwoPi;

            // 每40帧刷新四角十六进制读出
            hexUpdateClock++;
            if (hexUpdateClock >= 40) {
                hexUpdateClock = 0;
                for (int idx = 0; idx < cornerHex.Length; idx++) {
                    char[] buf = new char[4];
                    for (int c = 0; c < 4; c++)
                        buf[c] = HexChars[Main.rand.Next(HexChars.Length)];
                    cornerHex[idx] = $"0x{new string(buf)}";
                }
            }
        }

        private static void Advance(ref float t, float speed) {
            t += speed;
            if (t > MathHelper.TwoPi) t -= MathHelper.TwoPi;
        }

        public void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float fa = alpha * (0.92f + hoverGlow);

            // 多层扩散阴影
            for (int d = 7; d >= 1; d--) {
                Rectangle s = rect;
                s.Inflate(d, d);
                s.Offset(4, 5);
                spriteBatch.Draw(px, s, new Rectangle(0, 0, 1, 1),
                    Color.Black * (alpha * 0.06f * (7f - d) / 7f));
            }

            // 纵向渐变背景（深暗色调）
            int segs = 24;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                float pulse = MathF.Sin(circuitPulseTimer * 0.55f + t * 2.1f) * 0.5f + 0.5f;
                Color dark = new Color(4, 8, 18);
                Color mid = Color.Lerp(new Color(10, 20, 34), new Color(9, 20, 28), t * 0.5f);
                Color c = Color.Lerp(dark, mid, pulse) * (fa * 0.95f);
                spriteBatch.Draw(px, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)),
                    new Rectangle(0, 0, 1, 1), c);
            }

            // 对角线纹理（45°细线，间隔2行降低负担）
            int dspacing = 18;
            float dphase = dataStreamTimer * 14f;
            for (int col = -(rect.Height / dspacing) - 1; col < (rect.Width / dspacing) + 2; col++) {
                int ox = (int)(col * dspacing + dphase % dspacing);
                for (int row = 0; row < rect.Height; row += 2) {
                    int px2 = rect.X + ox - row;
                    if (px2 < rect.X || px2 >= rect.Right) continue;
                    spriteBatch.Draw(px, new Rectangle(px2, rect.Y + row, 1, 1),
                        new Rectangle(0, 0, 1, 1), new Color(18, 72, 82) * (fa * 0.032f));
                }
            }

            // 全息闪烁叠层
            float flicker = MathF.Sin(hologramFlicker * 1.6f) * 0.5f + 0.5f;
            spriteBatch.Draw(px, rect, new Rectangle(0, 0, 1, 1),
                new Color(0, 28, 36) * (fa * 0.18f * flicker));

            // 偶发故障横条
            float gf = MathF.Sin(glitchTimer * 2.1f);
            if (gf > 0.97f) {
                float gy = rect.Y + (glitchTimer * 97f % rect.Height);
                spriteBatch.Draw(px,
                    new Rectangle(rect.X + 5, (int)gy, rect.Width - 10, 1),
                    new Rectangle(0, 0, 1, 1),
                    new Color(0, 200, 195) * (fa * (gf - 0.97f) * 3.5f));
            }

            // 匀速扫描线（向下循环）
            float scanY = rect.Y + sweepTimer * rect.Height;
            for (int row = 0; row <= 3; row++) {
                float iy = scanY + row * 1.5f;
                if (iy < rect.Y || iy > rect.Bottom) continue;
                float fade = 1f - row * 0.28f;
                spriteBatch.Draw(px,
                    new Rectangle(rect.X + 6, (int)iy, rect.Width - 12, 1),
                    new Rectangle(0, 0, 1, 1),
                    new Color(0, 185, 200) * (fa * 0.2f * fade));
            }
        }

        public void DrawFrame(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float fa = alpha * (0.9f + hoverGlow * 0.3f);

            // 顶部主强调线（3px亮+1px暗）
            Color topBright = new Color(0, 218, 208) * (fa * 0.97f);
            Color topDim = new Color(0, 140, 160) * (fa * 0.45f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), topBright);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y + 3, rect.Width, 1), new Rectangle(0, 0, 1, 1), topDim);

            // 左侧强调竖条（渐变上亮下暗）
            int lbH = rect.Height / 2;
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 4, lbH),
                new Rectangle(0, 0, 1, 1), new Color(0, 200, 190) * (fa * 0.72f));
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y + lbH, 4, rect.Height - lbH),
                new Rectangle(0, 0, 1, 1), new Color(0, 130, 130) * (fa * 0.35f));

            // 右侧细线
            spriteBatch.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height),
                new Rectangle(0, 0, 1, 1), new Color(0, 95, 115) * (fa * 0.42f));
            // 底部细线
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), new Color(0, 115, 135) * (fa * 0.32f));

            // 顶部左侧刻痕
            spriteBatch.Draw(px, new Rectangle(rect.X + 4, rect.Y, 1, 9), new Rectangle(0, 0, 1, 1), topBright * 0.82f);
            spriteBatch.Draw(px, new Rectangle(rect.X + 18, rect.Y, 1, 6), new Rectangle(0, 0, 1, 1), topBright * 0.55f);
            spriteBatch.Draw(px, new Rectangle(rect.X + 32, rect.Y, 1, 4), new Rectangle(0, 0, 1, 1), topBright * 0.32f);

            // 右侧刻度尺
            DrawRuler(spriteBatch, rect, fa);

            // 四角十六进制读出
            DrawCornerHex(spriteBatch, rect, fa);
        }

        public Color GetNameGlowColor(float alpha) {
            return new Color(0, 220, 200) * (alpha * 0.75f);
        }

        public Color GetNameColor(float alpha) {
            return Color.Lerp(new Color(205, 245, 255), Color.White, 0.2f) * alpha;
        }

        public Color GetHintColor(float alpha, float blink) {
            return new Color(0, 210, 185) * (alpha * blink);
        }

        public void Reset() {
            circuitPulseTimer = 0f;
            hologramFlicker = 0f;
            dataStreamTimer = 0f;
            sweepTimer = 0f;
            glitchTimer = 0f;
            hexUpdateClock = 0;
            dataParticles.Clear();
            circuitNodes.Clear();
            dataParticleTimer = 0;
            circuitNodeTimer = 0;
        }

        public void GetParticles(out List<object> particles) {
            particles = [.. circuitNodes, .. dataParticles];
        }

        public void UpdateParticles(Vector2 basePos, float panelFade) {
            // 用面板中心估算面板范围
            Vector2 panelPos = new(basePos.X - 100f, basePos.Y - 50f);
            Vector2 panelSize = new(200f, 100f);

            dataParticleTimer++;
            if (panelFade > 0.6f && dataParticleTimer >= 25 && dataParticles.Count < 10) {
                dataParticleTimer = 0;
                Vector2 p = panelPos + new Vector2(
                    Main.rand.NextFloat(20f, panelSize.X - 20f),
                    Main.rand.NextFloat(10f, panelSize.Y - 10f));
                dataParticles.Add(new DraedonDataPRT(p));
            }
            for (int i = dataParticles.Count - 1; i >= 0; i--) {
                if (dataParticles[i].Update(panelPos, panelSize))
                    dataParticles.RemoveAt(i);
            }

            circuitNodeTimer++;
            if (panelFade > 0.6f && circuitNodeTimer >= 35 && circuitNodes.Count < 6) {
                circuitNodeTimer = 0;
                circuitNodes.Add(new CircuitNodePRT(
                    panelPos + new Vector2(
                        Main.rand.NextFloat(20f, panelSize.X - 20f),
                        Main.rand.NextFloat(10f, panelSize.Y - 10f))));
            }
            for (int i = circuitNodes.Count - 1; i >= 0; i--) {
                if (circuitNodes[i].Update(panelPos, panelSize))
                    circuitNodes.RemoveAt(i);
            }
        }

        /// <summary>
        /// 右侧刻度尺：等距短竖线，每4格加长，流光缓慢向下循环
        /// </summary>
        private void DrawRuler(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int rx = rect.Right - 8;
            int spacing = 10;
            int marks = rect.Height / spacing;
            float flow = circuitPulseTimer * 0.25f;
            for (int i = 0; i < marks; i++) {
                float t = (float)i / marks;
                float bright = MathF.Sin((t + flow) * MathHelper.TwoPi) * 0.3f + 0.45f;
                int mLen = (i % 4 == 0) ? 6 : 3;
                Color mc = new Color(0, 175, 168) * (alpha * bright);
                sb.Draw(px, new Rectangle(rx - mLen, rect.Y + i * spacing, mLen, 1),
                    new Rectangle(0, 0, 1, 1), mc);
            }
        }

        /// <summary>
        /// 四角十六进制读出：随机HEX地址串，每40帧刷新
        /// </summary>
        private void DrawCornerHex(SpriteBatch sb, Rectangle rect, float alpha) {
            if (alpha < 0.04f) return;
            float blink = MathF.Sin(circuitPulseTimer * 0.75f) * 0.18f + 0.82f;
            Color col = new Color(0, 155, 148) * (alpha * 0.55f * blink);
            float sc = 0.5f;
            var font = FontAssets.MouseText.Value;

            // 左上
            Utils.DrawBorderString(sb, cornerHex[0], new Vector2(rect.X + 5f, rect.Y + 5f), col, sc);
            // 右上（右对齐）
            float w1 = font.MeasureString(cornerHex[1]).X * sc;
            Utils.DrawBorderString(sb, cornerHex[1], new Vector2(rect.Right - w1 - 12f, rect.Y + 5f), col, sc);
            // 左下
            Utils.DrawBorderString(sb, cornerHex[2], new Vector2(rect.X + 5f, rect.Bottom - 14f), col * 0.68f, sc);
            // 右下（右对齐）
            float w3 = font.MeasureString(cornerHex[3]).X * sc;
            Utils.DrawBorderString(sb, cornerHex[3], new Vector2(rect.Right - w3 - 12f, rect.Bottom - 14f), col * 0.68f, sc);
        }
    }
}
