using CalamityOverhaul.Content.ADV.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.ADVChoices.Styles
{
    /// <summary>
    /// 嘉登终端选项样式
    /// </summary>
    internal class DraedonChoiceBoxStyle : IChoiceBoxStyle
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

        // 粒子系统（使用与对话框相同的共享粒子类型）
        private readonly List<DraedonDataPRT> dataParticles = [];
        private int dataParticleSpawnTimer;
        private readonly List<CircuitNodePRT> circuitNodes = [];
        private int circuitNodeSpawnTimer;
        private const float TechSideMargin = 22f;

        public void Update(Rectangle panelRect, bool active, bool closing) {
            Advance(ref circuitPulseTimer, 0.025f);
            Advance(ref hologramFlicker, 0.13f);
            Advance(ref dataStreamTimer, 0.022f);
            // 匀速扫描（永远向下循环，不使用正弦往返）
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

            // 数据粒子（生成间隔28帧，上限8个）
            float scaleW = Main.UIScale;
            dataParticleSpawnTimer++;
            if (active && !closing && dataParticleSpawnTimer >= 28 && dataParticles.Count < 8) {
                dataParticleSpawnTimer = 0;
                float left = panelRect.X + TechSideMargin * scaleW;
                float right = panelRect.Right - TechSideMargin * scaleW;
                Vector2 p = new(Main.rand.NextFloat(left, right),
                    panelRect.Y + Main.rand.NextFloat(30f, panelRect.Height - 30f));
                dataParticles.Add(new DraedonDataPRT(p));
            }
            for (int i = dataParticles.Count - 1; i >= 0; i--) {
                Vector2 panelPos = new(panelRect.X, panelRect.Y);
                Vector2 panelSize = new(panelRect.Width, panelRect.Height);
                if (dataParticles[i].Update(panelPos, panelSize))
                    dataParticles.RemoveAt(i);
            }

            // 电路节点（上限5个）
            circuitNodeSpawnTimer++;
            if (active && !closing && circuitNodeSpawnTimer >= 36 && circuitNodes.Count < 5) {
                circuitNodeSpawnTimer = 0;
                float left = panelRect.X + TechSideMargin * scaleW;
                float right = panelRect.Right - TechSideMargin * scaleW;
                circuitNodes.Add(new CircuitNodePRT(
                    new Vector2(Main.rand.NextFloat(left, right),
                                panelRect.Y + Main.rand.NextFloat(30f, panelRect.Height - 30f))));
            }
            for (int i = circuitNodes.Count - 1; i >= 0; i--) {
                Vector2 panelPos = new(panelRect.X, panelRect.Y);
                Vector2 panelSize = new(panelRect.Width, panelRect.Height);
                if (circuitNodes[i].Update(panelPos, panelSize))
                    circuitNodes.RemoveAt(i);
            }
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle panelRect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            // 多层扩散阴影
            for (int d = 8; d >= 1; d--) {
                Rectangle s = panelRect;
                s.Inflate(d, d);
                s.Offset(5, 6);
                spriteBatch.Draw(px, s, new Rectangle(0, 0, 1, 1),
                    Color.Black * (alpha * 0.055f * (8f - d) / 8f));
            }

            // 终端背景：深藏青渐变+对角线纹理+全息叠层
            DrawTerminalBackground(spriteBatch, panelRect, alpha);

            // 偶发故障横条
            float gf = MathF.Sin(glitchTimer * 2.1f);
            if (gf > 0.97f) {
                float gy = panelRect.Y + (glitchTimer * 97f % panelRect.Height);
                spriteBatch.Draw(px,
                    new Rectangle(panelRect.X + 5, (int)gy, panelRect.Width - 10, 1),
                    new Rectangle(0, 0, 1, 1),
                    new Color(0, 200, 195) * (alpha * (gf - 0.97f) * 3.5f));
            }

            // 匀速单条扫描线（永远向下循环）
            float scanY = panelRect.Y + sweepTimer * panelRect.Height;
            for (int row = 0; row <= 3; row++) {
                float iy = scanY + row * 1.5f;
                if (iy < panelRect.Y || iy > panelRect.Bottom) continue;
                float fade = 1f - row * 0.28f;
                spriteBatch.Draw(px,
                    new Rectangle(panelRect.X + 8, (int)iy, panelRect.Width - 16, 1),
                    new Rectangle(0, 0, 1, 1),
                    new Color(0, 185, 200) * (alpha * 0.2f * fade));
            }

            // 不对称边框
            DrawAsymmetricFrame(spriteBatch, panelRect, alpha);

            // 右侧刻度尺
            DrawRuler(spriteBatch, panelRect, alpha);

            // 四角十六进制读出
            DrawCornerHex(spriteBatch, panelRect, alpha);

            // 粒子
            foreach (var node in circuitNodes)
                node.Draw(spriteBatch, alpha * 0.72f);
            foreach (var particle in dataParticles)
                particle.Draw(spriteBatch, alpha * 0.62f);
        }

        public void DrawChoiceBackground(SpriteBatch spriteBatch, Rectangle choiceRect, bool enabled, float hoverProgress, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            // 选项背景（青绿色调）
            Color choiceBg = enabled
                ? Color.Lerp(new Color(4, 14, 22) * 0.3f, new Color(10, 32, 38) * 0.55f, hoverProgress)
                : new Color(8, 10, 14) * 0.15f;

            spriteBatch.Draw(px, choiceRect, new Rectangle(0, 0, 1, 1), choiceBg * alpha);

            Color techColor = GetEdgeColor(alpha);
            if (enabled && hoverProgress > 0.01f) {
                DrawChoiceBorder(spriteBatch, choiceRect, techColor * (hoverProgress * 0.6f));

                // 悬停时绘制虚线数据流指示
                DrawChoiceDashIndicator(spriteBatch, choiceRect, techColor, hoverProgress, alpha);
            }
            else if (!enabled) {
                DrawChoiceBorder(spriteBatch, choiceRect, new Color(0, 55, 65) * (alpha * 0.2f));
            }
        }

        public Color GetEdgeColor(float alpha) {
            float flicker = MathF.Sin(hologramFlicker * 1.5f) * 0.5f + 0.5f;
            return Color.Lerp(new Color(0, 175, 195), new Color(0, 220, 210), flicker) * (alpha * 0.85f);
        }

        public Color GetTextGlowColor(float alpha, float hoverProgress) {
            return GetEdgeColor(alpha);
        }

        public void DrawTitleDecoration(SpriteBatch spriteBatch, Vector2 titlePos, string title, float alpha) {
            // 名字辉光晕（青绿色）
            Color nameGlow = new Color(0, 220, 200) * (alpha * 0.75f);
            for (int i = 0; i < 4; i++) {
                float a = MathHelper.TwoPi * i / 4f;
                Vector2 off = a.ToRotationVector2() * 1.8f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + off, nameGlow * 0.55f, 0.95f);
            }

            // 「>」前缀指示符
            Utils.DrawBorderString(spriteBatch, ">",
                titlePos - new Vector2(14f, 0f),
                new Color(0, 255, 205) * (alpha * 0.9f),
                0.95f * 0.85f);

            // 名字下方短横线
            float nameW = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(title).X * 0.95f;
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle((int)titlePos.X, (int)(titlePos.Y + 20f), (int)(nameW * 0.6f), 1),
                new Rectangle(0, 0, 1, 1),
                new Color(0, 195, 180) * (alpha * 0.42f));
        }

        public void DrawDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha) {
            // 虚线分割，缓慢向右流动（与对话框统一风格）
            Texture2D px = VaultAsset.placeholder2.Value;
            float len = end.X - start.X;
            const int dashW = 5, gapW = 3;
            float flow = dataStreamTimer * 24f;
            float period = dashW + gapW;

            float x = start.X - (flow % period);
            while (x < end.X) {
                float segStart = Math.Max(x, start.X);
                float segEnd = Math.Min(x + dashW, end.X);
                if (segEnd > segStart) {
                    float t = (segStart - start.X) / len;
                    float bright = MathF.Sin(t * MathHelper.Pi) * 0.45f + 0.55f;
                    Color c = new Color(0, 175, 195) * (alpha * bright * 0.85f);
                    spriteBatch.Draw(px, new Rectangle((int)segStart, (int)start.Y, (int)(segEnd - segStart), 1),
                        new Rectangle(0, 0, 1, 1), c);
                }
                x += period;
            }
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
            dataParticleSpawnTimer = 0;
            circuitNodeSpawnTimer = 0;
            for (int i = 0; i < cornerHex.Length; i++)
                cornerHex[i] = "0x????";
        }

        #region 工具方法

        private static void Advance(ref float t, float speed) {
            t += speed;
            if (t > MathHelper.TwoPi) t -= MathHelper.TwoPi;
        }

        /// <summary>
        /// 终端背景：纵向深藏青渐变+对角线纹理+全息闪烁叠层
        /// </summary>
        private void DrawTerminalBackground(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            // 纵向渐变（28段，轻微脉冲呼吸）
            int segs = 28;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                float pulse = MathF.Sin(circuitPulseTimer * 0.55f + t * 2.1f) * 0.5f + 0.5f;
                Color dark = new Color(4, 8, 18);
                Color mid = Color.Lerp(new Color(10, 20, 34), new Color(9, 20, 28), t * 0.5f);
                Color c = Color.Lerp(dark, mid, pulse) * (alpha * 0.95f);
                sb.Draw(px, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)),
                    new Rectangle(0, 0, 1, 1), c);
            }

            // 对角线纹理（45°，间隔2行跳过降低绘制压力）
            int dspacing = 18;
            float dphase = dataStreamTimer * 14f;
            for (int col = -(rect.Height / dspacing) - 1; col < (rect.Width / dspacing) + 2; col++) {
                int ox = (int)(col * dspacing + dphase % dspacing);
                for (int row = 0; row < rect.Height; row += 2) {
                    int px2 = rect.X + ox - row;
                    if (px2 < rect.X || px2 >= rect.Right) continue;
                    sb.Draw(px, new Rectangle(px2, rect.Y + row, 1, 1),
                        new Rectangle(0, 0, 1, 1), new Color(18, 72, 82) * (alpha * 0.032f));
                }
            }

            // 全息闪烁叠层
            float flicker = MathF.Sin(hologramFlicker * 1.6f) * 0.5f + 0.5f;
            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1),
                new Color(0, 28, 36) * (alpha * 0.18f * flicker));
        }

        /// <summary>
        /// 不对称边框：顶部双层重线+左侧强调竖条+细右底线+顶部左侧刻痕
        /// </summary>
        private static void DrawAsymmetricFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            // 顶部主强调线（3px+1px双层）
            Color topBright = new Color(0, 218, 208) * (alpha * 0.97f);
            Color topDim = new Color(0, 140, 160) * (alpha * 0.45f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), topBright);
            sb.Draw(px, new Rectangle(rect.X, rect.Y + 3, rect.Width, 1), new Rectangle(0, 0, 1, 1), topDim);

            // 左侧强调竖条（4px全高，渐变上亮下暗）
            int lbH = rect.Height / 2;
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 4, lbH), new Rectangle(0, 0, 1, 1),
                new Color(0, 200, 190) * (alpha * 0.72f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y + lbH, 4, rect.Height - lbH), new Rectangle(0, 0, 1, 1),
                new Color(0, 130, 130) * (alpha * 0.35f));

            // 右侧细线
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height),
                new Rectangle(0, 0, 1, 1), new Color(0, 95, 115) * (alpha * 0.42f));
            // 底部细线
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), new Color(0, 115, 135) * (alpha * 0.32f));

            // 顶部左侧刻痕
            sb.Draw(px, new Rectangle(rect.X + 4, rect.Y, 1, 9), new Rectangle(0, 0, 1, 1), topBright * 0.82f);
            sb.Draw(px, new Rectangle(rect.X + 18, rect.Y, 1, 6), new Rectangle(0, 0, 1, 1), topBright * 0.55f);
            sb.Draw(px, new Rectangle(rect.X + 32, rect.Y, 1, 4), new Rectangle(0, 0, 1, 1), topBright * 0.32f);
        }

        /// <summary>
        /// 右侧刻度尺：等距短竖线，每4格加长，流光缓慢向下循环
        /// </summary>
        private void DrawRuler(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int rx = rect.Right - 10;
            int spacing = 12;
            int marks = rect.Height / spacing;
            float flow = circuitPulseTimer * 0.25f;
            for (int i = 0; i < marks; i++) {
                float t = (float)i / marks;
                float bright = MathF.Sin((t + flow) * MathHelper.TwoPi) * 0.3f + 0.45f;
                int mLen = (i % 4 == 0) ? 7 : 4;
                Color mc = new Color(0, 175, 168) * (alpha * bright);
                sb.Draw(px, new Rectangle(rx - mLen, rect.Y + i * spacing, mLen, 1),
                    new Rectangle(0, 0, 1, 1), mc);
            }
        }

        /// <summary>
        /// 四角十六进制读出：随机HEX地址串，每40帧刷新，轻微闪烁
        /// </summary>
        private void DrawCornerHex(SpriteBatch sb, Rectangle rect, float alpha) {
            if (alpha < 0.04f) return;
            float blink = MathF.Sin(circuitPulseTimer * 0.75f) * 0.18f + 0.82f;
            Color col = new Color(0, 155, 148) * (alpha * 0.55f * blink);
            float sc = 0.55f;
            var font = Terraria.GameContent.FontAssets.MouseText.Value;

            // 左上
            Utils.DrawBorderString(sb, cornerHex[0], new Vector2(rect.X + 6f, rect.Y + 6f), col, sc);
            // 右上（右对齐）
            float w1 = font.MeasureString(cornerHex[1]).X * sc;
            Utils.DrawBorderString(sb, cornerHex[1], new Vector2(rect.Right - w1 - 14f, rect.Y + 6f), col, sc);
            // 左下
            Utils.DrawBorderString(sb, cornerHex[2], new Vector2(rect.X + 6f, rect.Bottom - 16f), col * 0.68f, sc);
            // 右下（右对齐）
            float w3 = font.MeasureString(cornerHex[3]).X * sc;
            Utils.DrawBorderString(sb, cornerHex[3], new Vector2(rect.Right - w3 - 14f, rect.Bottom - 16f), col * 0.68f, sc);
        }

        /// <summary>
        /// 选项悬停时的虚线数据流指示（左侧竖向虚线）
        /// </summary>
        private void DrawChoiceDashIndicator(SpriteBatch sb, Rectangle choiceRect, Color techColor, float hoverProgress, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            const int dashH = 4, gapH = 3;
            float flow = dataStreamTimer * 20f;
            float period = dashH + gapH;
            Color dashColor = techColor * (hoverProgress * 0.35f);

            float y = choiceRect.Y - (flow % period);
            while (y < choiceRect.Bottom) {
                float segStart = Math.Max(y, choiceRect.Y);
                float segEnd = Math.Min(y + dashH, choiceRect.Bottom);
                if (segEnd > segStart) {
                    sb.Draw(px,
                        new Rectangle(choiceRect.X, (int)segStart, 2, (int)(segEnd - segStart)),
                        new Rectangle(0, 0, 1, 1), dashColor);
                }
                y += period;
            }
        }

        private static void DrawChoiceBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D px = VaultAsset.placeholder2.Value;

            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height),
                new Rectangle(0, 0, 1, 1), color);
        }

        #endregion
    }
}
