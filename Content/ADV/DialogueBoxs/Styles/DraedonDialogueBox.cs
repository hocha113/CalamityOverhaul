using CalamityOverhaul.Content.ADV.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.DialogueBoxs.Styles
{
    /// <summary>
    /// 嘉登终端风格对话框
    /// </summary>
    internal class DraedonDialogueBox : DialogueBoxBase
    {
        public static DraedonDialogueBox Instance => UIHandleLoader.GetUIHandleOfType<DraedonDialogueBox>();
        public override string LocalizationCategory => "UI";

        // 风格参数
        private const float FixedWidth = 540f;
        protected override float PanelWidth => FixedWidth;

        // 动画计时
        private float circuitPulseTimer;   // 边框/发光脉冲
        private float hologramFlicker;     // 全息闪烁
        private float dataStreamTimer;     // 虚线/纹理流动
        private float sweepTimer;          // 扫描线
        private float glitchTimer;         // 故障横条

        // 四角 HEX 读出
        private readonly string[] cornerHex = ["0x????", "0x????", "0x????", "0x????"];
        private int hexUpdateClock;
        private static readonly char[] HexChars = "0123456789ABCDEF".ToCharArray();

        // 粒子
        private readonly List<DraedonDataPRT> dataParticles = [];
        private int dataParticleSpawnTimer;
        private readonly List<CircuitNodePRT> circuitNodes = [];
        private int circuitNodeSpawnTimer;
        private const float TechSideMargin = 28f;

        #region 样式配置

        protected override float PortraitScaleMin => 0.9f;
        protected override float TopNameOffsetBase => 12f;
        protected override float TextBlockOffsetBase => 38f;
        protected override float NameScale => 0.95f;
        protected override float TextScale => 0.82f;
        protected override float NameGlowRadius => 2f;
        protected override float PortraitAvailHeightOffset => 50f;
        protected override float PortraitMinHeight => 100f;
        protected override float PortraitMaxHeight => 270f;
        protected override float PortraitFramePadding => 6f;
        protected override float PortraitGlowPadding => 3f;
        protected override float PortraitLeftMargin => 22f;
        protected override float ContinueHintScale => 0.82f;
        protected override float FastHintScale => 0.72f;

        protected override Color GetSilhouetteColor(ContentDrawContext ctx)
            => new Color(20, 35, 55) * 0.85f;

        // 文字行无偏移，终端输出稳定
        protected override Vector2 ApplyTextLineOffset(ContentDrawContext ctx, Vector2 basePosition, int lineIndex)
            => basePosition;

        protected override Color GetTextLineColor(ContentDrawContext ctx, int lineIndex)
            => Color.Lerp(new Color(205, 245, 255), Color.White, 0.2f) * ctx.ContentAlpha;

        // 青绿提示色，区别于来电冰蓝
        protected override Color GetContinueHintColor(ContentDrawContext ctx, float blink)
            => new Color(0, 210, 185) * (blink * ctx.ContentAlpha);

        protected override Color GetFastHintColor(ContentDrawContext ctx)
            => new Color(0, 165, 145) * (0.5f * ctx.ContentAlpha);

        #endregion

        #region 绘制实现

        /// <summary>
        /// 斜切头像框：右上角削角 + 底部左圆角电路迹线<br/>
        /// 区别于来电框的四角L括号，这里是"不规则切面"感
        /// </summary>
        protected override void DrawPortraitFrame(ContentDrawContext ctx, Rectangle frameRect) {
            SpriteBatch sb = ctx.SpriteBatch;
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = ctx.Alpha * ctx.PortraitData.Fade * ctx.PortraitExtraAlpha;

            // 背景
            sb.Draw(px, frameRect, new Rectangle(0, 0, 1, 1),
                new Color(5, 12, 26) * (alpha * 0.92f));

            // 主矩形边框（比来电框稍细，1px主线）
            Color edge = new Color(28, 160, 230) * (alpha * 0.75f);
            int bw = 2;
            DrawRect(sb, px, frameRect, bw, edge);

            // 右上角削角（三角形遮盖，再补斜线高亮边）
            int cut = Math.Max(4, frameRect.Width / 4);
            // 遮盖三角
            for (int row = 0; row < cut; row++) {
                int segLen = cut - row;
                sb.Draw(px,
                    new Rectangle(frameRect.Right - segLen - bw, frameRect.Y + row, segLen, 1),
                    new Rectangle(0, 0, 1, 1),
                    new Color(5, 12, 26) * alpha);
            }
            // 斜线高亮边（颜色比内框亮——不对称装饰感）
            Color diagEdge = new Color(0, 210, 205) * (alpha * 0.95f);
            for (int row = 0; row < cut; row++) {
                float fade = 1f - (float)row / cut;
                sb.Draw(px,
                    new Rectangle(frameRect.Right - (cut - row) - bw, frameRect.Y + row, 2, 1),
                    new Rectangle(0, 0, 1, 1),
                    diagEdge * fade);
            }

            // 左下角电路迹线装饰（区别于来电框右上角脉冲弧）
            DrawCornerTrace(sb, px, new Vector2(frameRect.X + bw, frameRect.Bottom - bw), alpha);
        }

        /// <summary>
        /// 头像青绿色辉光（与来电框蓝白色系视觉区分）
        /// </summary>
        protected override void DrawPortraitGlow(ContentDrawContext ctx, Rectangle glowRect) {
            SpriteBatch sb = ctx.SpriteBatch;
            Texture2D px = VaultAsset.placeholder2.Value;
            float pulse = MathF.Sin(circuitPulseTimer * 1.3f) * 0.3f + 0.7f;
            Color glow = new Color(0, 195, 175) *
                (ctx.ContentAlpha * 0.5f * pulse * ctx.PortraitData.Fade * ctx.PortraitExtraAlpha);

            sb.Draw(px, glowRect, new Rectangle(0, 0, 1, 1), glow * 0.13f);
            int b = 2;
            sb.Draw(px, new Rectangle(glowRect.X, glowRect.Y, glowRect.Width, b), new Rectangle(0, 0, 1, 1), glow * 0.75f);
            sb.Draw(px, new Rectangle(glowRect.X, glowRect.Bottom - b, glowRect.Width, b), new Rectangle(0, 0, 1, 1), glow * 0.45f);
            sb.Draw(px, new Rectangle(glowRect.X, glowRect.Y, b, glowRect.Height), new Rectangle(0, 0, 1, 1), glow * 0.60f);
            sb.Draw(px, new Rectangle(glowRect.Right - b, glowRect.Y, b, glowRect.Height), new Rectangle(0, 0, 1, 1), glow * 0.60f);
        }

        /// <summary>
        /// 名称装饰：`>` 前缀 + 名字下方短横线标记<br/>
        /// 区别于来电框的频道标签/信号格标识
        /// </summary>
        protected override void DrawNameGlow(ContentDrawContext ctx, Vector2 position, float alpha) {
            SpriteBatch sb = ctx.SpriteBatch;

            // 名字辉光晕（青绿色替代冰蓝）
            Color nameGlow = new Color(0, 220, 200) * (alpha * 0.75f);
            for (int i = 0; i < NameGlowCount; i++) {
                float a = MathHelper.TwoPi * i / NameGlowCount;
                Vector2 off = a.ToRotationVector2() * NameGlowRadius * ctx.SwitchEase;
                Utils.DrawBorderString(sb, current.Speaker, position + off, nameGlow * 0.55f, NameScale);
            }

            // `>` 前缀指示符（在名字左侧，代替来电框的"通道"标识）
            Utils.DrawBorderString(sb, ">",
                position - new Vector2(14f, 0f),
                new Color(0, 255, 205) * (alpha * 0.9f),
                NameScale * 0.85f);

            // 名字下方短横线（轻量 accent，替代来电框连接进度条）
            float nameW = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(current.Speaker).X * NameScale;
            sb.Draw(VaultAsset.placeholder2.Value,
                new Rectangle((int)position.X, (int)(position.Y + 20f), (int)(nameW * 0.65f), 1),
                new Rectangle(0, 0, 1, 1),
                new Color(0, 195, 180) * (alpha * 0.45f));
        }

        /// <summary>
        /// 虚线分割线，缓慢向右流动<br/>
        /// 区别于来电框的流动实心渐变线
        /// </summary>
        protected override void DrawDividerLine(ContentDrawContext ctx, Vector2 start, Vector2 end, float alpha) {
            SpriteBatch sb = ctx.SpriteBatch;
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
                    sb.Draw(px, new Rectangle((int)segStart, (int)start.Y, (int)(segEnd - segStart), 1),
                        new Rectangle(0, 0, 1, 1), c);
                }
                x += period;
            }
        }

        #endregion

        protected override void StyleUpdate(Vector2 panelPos, Vector2 panelSize) {
            Advance(ref circuitPulseTimer, 0.025f);
            Advance(ref hologramFlicker, 0.13f);
            Advance(ref dataStreamTimer, 0.022f);
            // 匀速扫描（不是来电框的正弦往返，而是永远向下循环）
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

            // 粒子（生成间隔30帧，上限10个，密度约为来电框的2/3）
            dataParticleSpawnTimer++;
            if (Active && dataParticleSpawnTimer >= 30 && dataParticles.Count < 10) {
                dataParticleSpawnTimer = 0;
                Vector2 p = panelPos + new Vector2(
                    Main.rand.NextFloat(TechSideMargin, panelSize.X - TechSideMargin),
                    Main.rand.NextFloat(40f, panelSize.Y - 40f));
                dataParticles.Add(new DraedonDataPRT(p));
            }
            for (int i = dataParticles.Count - 1; i >= 0; i--) {
                if (dataParticles[i].Update(panelPos, panelSize))
                    dataParticles.RemoveAt(i);
            }

            // 电路节点（上限6个）
            circuitNodeSpawnTimer++;
            if (Active && circuitNodeSpawnTimer >= 38 && circuitNodes.Count < 6) {
                circuitNodeSpawnTimer = 0;
                float scaleW = Main.UIScale;
                float left = panelPos.X + TechSideMargin * scaleW;
                float right = panelPos.X + panelSize.X - TechSideMargin * scaleW;
                circuitNodes.Add(new CircuitNodePRT(
                    new Vector2(Main.rand.NextFloat(left, right),
                                panelPos.Y + Main.rand.NextFloat(40f, panelSize.Y - 40f))));
            }
            for (int i = circuitNodes.Count - 1; i >= 0; i--) {
                if (circuitNodes[i].Update(panelPos, panelSize))
                    circuitNodes.RemoveAt(i);
            }
        }

        private static void Advance(ref float t, float speed) {
            t += speed;
            if (t > MathHelper.TwoPi) t -= MathHelper.TwoPi;
        }

        protected override void DrawStyle(SpriteBatch spriteBatch, Rectangle panelRect, float alpha,
            float contentAlpha, float easedProgress) {
            // ── 多层扩散阴影（宽于来电框，位移略不同） ──────
            for (int d = 9; d >= 1; d--) {
                Rectangle s = panelRect;
                s.Inflate(d, d);
                s.Offset(5, 6);
                spriteBatch.Draw(VaultAsset.placeholder2.Value, s, new Rectangle(0, 0, 1, 1),
                    Color.Black * (alpha * 0.055f * (9f - d) / 9f));
            }

            // ── 背景：深藏青渐变 + 对角线纹理（替代六边形点阵）
            DrawTerminalBackground(spriteBatch, panelRect, alpha);

            // ── 偶发故障横条（触发频率低于来电框，约1/5）───
            float gf = MathF.Sin(glitchTimer * 2.1f);
            if (gf > 0.97f) {
                float gy = panelRect.Y + (glitchTimer * 97f % panelRect.Height);
                spriteBatch.Draw(VaultAsset.placeholder2.Value,
                    new Rectangle(panelRect.X + 5, (int)gy, panelRect.Width - 10, 1),
                    new Rectangle(0, 0, 1, 1),
                    new Color(0, 200, 195) * (alpha * (gf - 0.97f) * 3.5f));
            }

            // ── 匀速单条扫描线（永远向下循环，区别于来电框的带拖影正弦线）──
            float scanY = panelRect.Y + sweepTimer * panelRect.Height;
            for (int row = 0; row <= 3; row++) {
                float iy = scanY + row * 1.5f;
                if (iy < panelRect.Y || iy > panelRect.Bottom) continue;
                float fade = 1f - row * 0.28f;
                spriteBatch.Draw(VaultAsset.placeholder2.Value,
                    new Rectangle(panelRect.X + 8, (int)iy, panelRect.Width - 16, 1),
                    new Rectangle(0, 0, 1, 1),
                    new Color(0, 185, 200) * (alpha * 0.2f * fade));
            }

            // ── 不对称边框（区别于来电框的对称L角括号）──────
            DrawAsymmetricFrame(spriteBatch, panelRect, alpha);

            // ── 右侧刻度尺（区别于来电框的信号格）──────────
            DrawRuler(spriteBatch, panelRect, alpha);

            // ── 四角十六进制读出（区别于来电框的均衡器/扇弧）
            DrawCornerHex(spriteBatch, panelRect, alpha);

            // ── 粒子 ─────────────────────────────────────
            foreach (var node in circuitNodes)
                node.Draw(spriteBatch, alpha * 0.78f);
            foreach (var particle in dataParticles)
                particle.Draw(spriteBatch, alpha * 0.68f);

            DrawTimedProgressIndicator(spriteBatch, panelRect, alpha);

            if (current == null || contentAlpha <= 0.01f)
                return;

            DrawPortraitAndText(spriteBatch, panelRect, alpha, contentAlpha);
        }

        #region 样式工具函数

        /// <summary>
        /// 终端背景：纵向渐变（深蓝→深蓝绿）+ 对角线纹理（细45°线）+ 全息闪烁叠层
        /// </summary>
        private void DrawTerminalBackground(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            // ── 纵向渐变（分28段，轻微脉冲呼吸）──
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

            // ── 对角线纹理（45°，逐行单像素，替代六边形点阵）──
            //    每px绘制成本高，用间隔2行跳过来降低draw call
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

            // ── 全息闪烁叠层 ──
            float flicker = MathF.Sin(hologramFlicker * 1.6f) * 0.5f + 0.5f;
            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1),
                new Color(0, 28, 36) * (alpha * 0.18f * flicker));
        }

        /// <summary>
        /// 不对称边框：顶部2层重线（3px亮+1px暗）+ 左侧4px强调竖条 + 顶部刻痕 + 细右/底线<br/>
        /// 来电框用对称四角L括号，这里用单侧重点边——完全不同的设计语言
        /// </summary>
        private static void DrawAsymmetricFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            // 顶部主强调线（3px+1px双层）
            Color topBright = new Color(0, 218, 208) * (alpha * 0.97f);
            Color topDim = new Color(0, 140, 160) * (alpha * 0.45f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), topBright);
            sb.Draw(px, new Rectangle(rect.X, rect.Y + 3, rect.Width, 1), new Rectangle(0, 0, 1, 1), topDim);

            // 左侧强调竖条（4px全高，渐变：上亮下暗）
            int lbH = rect.Height / 2;
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 4, lbH), new Rectangle(0, 0, 1, 1), new Color(0, 200, 190) * (alpha * 0.72f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y + lbH, 4, rect.Height - lbH), new Rectangle(0, 0, 1, 1), new Color(0, 130, 130) * (alpha * 0.35f));

            // 右侧细线
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height),
                new Rectangle(0, 0, 1, 1), new Color(0, 95, 115) * (alpha * 0.42f));
            // 底部细线
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), new Color(0, 115, 135) * (alpha * 0.32f));

            // 顶部左侧刻痕（机械感，来电框没有这个）
            sb.Draw(px, new Rectangle(rect.X + 4, rect.Y, 1, 9), new Rectangle(0, 0, 1, 1), topBright * 0.82f);
            sb.Draw(px, new Rectangle(rect.X + 18, rect.Y, 1, 6), new Rectangle(0, 0, 1, 1), topBright * 0.55f);
            sb.Draw(px, new Rectangle(rect.X + 32, rect.Y, 1, 4), new Rectangle(0, 0, 1, 1), topBright * 0.32f);
        }

        /// <summary>
        /// 右侧刻度尺：等距短竖线，每4格加长，流光缓慢向下循环<br/>
        /// 区别于来电框的信号格（格数=信号强度），这里是"坐标轴"感
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
        /// 四角十六进制读出：随机16进制地址串，每40帧刷新一次，轻微闪烁<br/>
        /// 替代来电框的均衡器条/扇形信号弧——完全不同的装饰元素
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
        /// 矩形线框（一次性画4条边）
        /// </summary>
        private static void DrawRect(SpriteBatch sb, Texture2D px, Rectangle r, int bw, Color c) {
            sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, bw), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(px, new Rectangle(r.X, r.Bottom - bw, r.Width, bw), new Rectangle(0, 0, 1, 1), c * 0.7f);
            sb.Draw(px, new Rectangle(r.X, r.Y, bw, r.Height), new Rectangle(0, 0, 1, 1), c * 0.85f);
            sb.Draw(px, new Rectangle(r.Right - bw, r.Y, bw, r.Height), new Rectangle(0, 0, 1, 1), c * 0.85f);
        }

        /// <summary>
        /// 头像框左下角电路迹线（L形小装饰，区别于来电框右上角的脉冲弧线）
        /// </summary>
        private static void DrawCornerTrace(SpriteBatch sb, Texture2D px, Vector2 origin, float alpha) {
            Color c = new Color(0, 175, 195) * (alpha * 0.62f);
            // 向右水平段
            sb.Draw(px, new Rectangle((int)origin.X, (int)origin.Y - 1, 16, 1), new Rectangle(0, 0, 1, 1), c);
            // 向右末端竖线（短）
            sb.Draw(px, new Rectangle((int)origin.X + 14, (int)origin.Y - 5, 2, 4), new Rectangle(0, 0, 1, 1), c * 0.65f);
        }

        #endregion
    }
}
