using CalamityOverhaul.Content.ADV.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.ADV.ADVRewardPopups.Styles
{
    /// <summary>
    /// SHPC赛博朋克风格奖励弹窗<br/>
    /// 复用 <see cref="CyberShaderPanel"/> (CyberPanel.fx) 驱动面板背景,<br/>
    /// 失败时降级为CPU程序化渐变+扫描线+六角点阵+扫掠光带<br/>
    /// 与SHPCDialogueBox/SHPCChoiceBoxStyle保持统一的霓虹蓝紫视觉语言
    /// </summary>
    internal class SHPCRewardStyle : IRewardPopupStyle
    {
        private float neonPulseTimer;
        private float dataFlowTimer;
        private float sweepTimer;
        //着色器专用单调递增时间
        private float shaderTime;

        //左侧数据流线相位(2条,与对话框/选项框一致)
        private readonly float[] dataLinePhases = new float[2];

        //四角状态文字
        private readonly string[] cornerStatus = ["LINK.OK", "SYS:RDY", "ITEM++", "ACK.."];
        private int statusUpdateClock;
        private static readonly string[] StatusPool = [
            "MAID.OK", "SYS:RDY", "LINK.UP", "ACT:ON", "v2.07b",
            "NRG:98%", "SYNC..", "CORE:A+", "NET.OK", "STB:Hi",
            "IO:PASS", "CHK:OK", "MOD:RUN", "BUF:CLR", "SIG:99",
            "ITEM++", "REW.OK", "DROP.OK", "ACK..", "STK:Hi"
        ];

        //粒子系统(精简数量,适配小面板)
        private readonly List<NeonMaidPRT> neonParticles = [];
        private int neonParticleSpawnTimer;
        private readonly List<CircuitNodePRT> circuitNodes = [];
        private int circuitNodeSpawnTimer;
        private const float SideMargin = 18f;

        //六角溢出边距(shader控制alpha形状)——小面板取较小值
        private const int ShaderEdgePad = 14;

        //主色调常量(与SHPCDialogueBox/SHPCChoiceBoxStyle统一)
        private static readonly Color NeonBlue = new(60, 120, 255);
        private static readonly Color NeonBlueDim = new(40, 60, 180);
        private static readonly Color DeepPurple = new(100, 40, 200);
        private static readonly Color PanelDark = new(10, 6, 22);

        public void Update(Rectangle panelRect, bool active, bool closing) {
            Advance(ref neonPulseTimer, 0.028f);
            Advance(ref dataFlowTimer, 0.018f);
            sweepTimer += 0.004f;
            if (sweepTimer > 100f) sweepTimer -= 100f;
            shaderTime += 0.016f;
            if (shaderTime > 10000f) shaderTime -= 10000f;

            for (int i = 0; i < dataLinePhases.Length; i++)
                dataLinePhases[i] = (dataLinePhases[i] + 0.014f + i * 0.005f) % 1f;

            statusUpdateClock++;
            if (statusUpdateClock >= 55) {
                statusUpdateClock = 0;
                for (int i = 0; i < cornerStatus.Length; i++)
                    cornerStatus[i] = StatusPool[Main.rand.Next(StatusPool.Length)];
            }
        }

        public void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //外阴影(紫色调,与对话框一致,3层精简)
            for (int d = 5; d >= 1; d--) {
                Rectangle s = rect;
                s.Inflate(d, d);
                s.Offset(3, 4);
                spriteBatch.Draw(px, s, new Rectangle(0, 0, 1, 1),
                    new Color(6, 3, 12) * (alpha * 0.10f * (5f - d) / 5f));
            }

            if (CyberShaderPanel.Available) {
                //hoverGlow转为轻微蓝紫提亮,避免过曝
                float bright = MathHelper.Clamp(0.95f + hoverGlow * 0.30f, 0.0f, 1.4f);
                Color tint = new Color(
                    (byte)Math.Min(255, (int)(225 * bright)),
                    (byte)Math.Min(255, (int)(225 * bright)),
                    (byte)Math.Min(255, (int)(255 * bright)),
                    (byte)255);
                CyberShaderPanel.Draw(spriteBatch, rect, alpha * 0.97f, shaderTime, ShaderEdgePad, tint);
            }
            else {
                DrawFallbackPanel(spriteBatch, rect, alpha, hoverGlow);
            }

            //左侧数据流线(shader和降级路径都叠加,保持视觉一致)
            DrawDataFlowLines(spriteBatch, rect, alpha);
        }

        //降级面板:无shader环境使用CPU堆叠绘制
        private void DrawFallbackPanel(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float fa = alpha * (0.92f + hoverGlow);

            //纯渐变背景(12段平滑,深暗紫色调)
            int segs = 12;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Color c = Color.Lerp(new Color(16, 8, 28), new Color(8, 5, 20), t) * (fa * 0.97f);
                spriteBatch.Draw(px, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)),
                    new Rectangle(0, 0, 1, 1), c);
            }

            //扫描线(每3px一条暗带)
            Color scanColor = new Color(20, 12, 45) * (fa * 0.10f);
            for (int y = rect.Y; y < rect.Bottom; y += 3)
                spriteBatch.Draw(px, new Rectangle(rect.X + 4, y, rect.Width - 8, 1),
                    new Rectangle(0, 0, 1, 1), scanColor);

            //简易六角点阵(用错行圆点模拟六角网格节点)
            int dotSpacingX = 16;
            int dotSpacingY = 14;
            Color dotColor = new Color(40, 25, 80) * (fa * 0.12f);
            for (int row = 0; row < rect.Height / dotSpacingY; row++) {
                int dy = rect.Y + row * dotSpacingY + 5;
                if (dy >= rect.Bottom - 4) continue;
                int offsetX = (row % 2 == 0) ? 0 : dotSpacingX / 2;
                for (int col = 0; col < rect.Width / dotSpacingX + 1; col++) {
                    int dx = rect.X + col * dotSpacingX + offsetX + 4;
                    if (dx >= rect.Right - 4) continue;
                    spriteBatch.Draw(px, new Rectangle(dx, dy, 1, 1),
                        new Rectangle(0, 0, 1, 1), dotColor);
                }
            }

            //扫掠光带(向下循环)
            float scanY = rect.Y + (sweepTimer * 0.1f % 1f) * rect.Height;
            for (int dy = -4; dy <= 4; dy++) {
                int py = (int)scanY + dy;
                if (py < rect.Y || py >= rect.Bottom) continue;
                float fade = 1f - Math.Abs(dy) / 5f;
                spriteBatch.Draw(px, new Rectangle(rect.X + 4, py, rect.Width - 8, 1),
                    new Rectangle(0, 0, 1, 1), NeonBlueDim * (fa * 0.12f * fade * fade));
            }

            //内发光脉冲
            float glowPulse = MathF.Sin(neonPulseTimer * 1.5f) * 0.5f + 0.5f;
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            spriteBatch.Draw(px, inner, new Rectangle(0, 0, 1, 1),
                NeonBlueDim * (fa * (0.10f + hoverGlow * 0.4f) * (0.5f + glowPulse * 0.5f)));

            //暗角(左右两侧渐暗)
            int vigW = 14;
            for (int v = 0; v < vigW; v += 4) {
                float vFade = (1f - (float)v / vigW) * 0.1f;
                Color vColor = new Color(4, 2, 8) * (fa * vFade);
                spriteBatch.Draw(px, new Rectangle(rect.X + v, rect.Y, 2, rect.Height),
                    new Rectangle(0, 0, 1, 1), vColor);
                spriteBatch.Draw(px, new Rectangle(rect.Right - v - 2, rect.Y, 2, rect.Height),
                    new Rectangle(0, 0, 1, 1), vColor);
            }
        }

        public void DrawFrame(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow) {
            //角括号装饰
            DrawCornerBrackets(spriteBatch, rect, alpha, hoverGlow);

            //四角状态文字
            DrawCornerStatusText(spriteBatch, rect, alpha);
        }

        public Color GetNameGlowColor(float alpha) {
            return NeonBlue * (alpha * 0.55f);
        }

        public Color GetNameColor(float alpha) {
            return Color.Lerp(new Color(210, 220, 255), Color.White, 0.2f) * alpha;
        }

        public Color GetHintColor(float alpha, float blink) {
            return NeonBlue * (alpha * blink * 0.85f);
        }

        public void Reset() {
            neonPulseTimer = 0f;
            dataFlowTimer = 0f;
            sweepTimer = 0f;
            shaderTime = 0f;
            statusUpdateClock = 0;
            neonParticles.Clear();
            circuitNodes.Clear();
            neonParticleSpawnTimer = 0;
            circuitNodeSpawnTimer = 0;
            for (int i = 0; i < dataLinePhases.Length; i++)
                dataLinePhases[i] = 0f;
            cornerStatus[0] = "LINK.OK";
            cornerStatus[1] = "SYS:RDY";
            cornerStatus[2] = "ITEM++";
            cornerStatus[3] = "ACK..";
        }

        public void GetParticles(out List<object> particles) {
            particles = [.. circuitNodes, .. neonParticles];
        }

        public void UpdateParticles(Vector2 basePos, float panelFade) {
            //用面板中心估算面板范围(与ADVRewardPopup.Draw中保持一致:240x132)
            Vector2 panelPos = new(basePos.X - 120f, basePos.Y - 66f);
            Vector2 panelSize = new(240f, 132f);

            neonParticleSpawnTimer++;
            if (panelFade > 0.6f && neonParticleSpawnTimer >= 28 && neonParticles.Count < 7) {
                neonParticleSpawnTimer = 0;
                Vector2 p = new(
                    Main.rand.NextFloat(panelPos.X + SideMargin, panelPos.X + panelSize.X - SideMargin),
                    Main.rand.NextFloat(panelPos.Y + 16f, panelPos.Y + panelSize.Y - 16f));
                neonParticles.Add(new NeonMaidPRT(p));
            }
            for (int i = neonParticles.Count - 1; i >= 0; i--) {
                if (neonParticles[i].Update(panelPos, panelSize))
                    neonParticles.RemoveAt(i);
            }

            circuitNodeSpawnTimer++;
            if (panelFade > 0.6f && circuitNodeSpawnTimer >= 42 && circuitNodes.Count < 4) {
                circuitNodeSpawnTimer = 0;
                Vector2 p = new(
                    Main.rand.NextFloat(panelPos.X + SideMargin, panelPos.X + panelSize.X - SideMargin),
                    Main.rand.NextFloat(panelPos.Y + 16f, panelPos.Y + panelSize.Y - 16f));
                circuitNodes.Add(new CircuitNodePRT(p));
            }
            for (int i = circuitNodes.Count - 1; i >= 0; i--) {
                if (circuitNodes[i].Update(panelPos, panelSize))
                    circuitNodes.RemoveAt(i);
            }
        }

        #region 样式工具函数

        private static void Advance(ref float t, float speed) {
            t += speed;
            if (t > MathHelper.TwoPi) t -= MathHelper.TwoPi;
        }

        /// <summary>
        /// 左侧数据流线(2条竖向霓虹流动线 + 侧翼辉光 + 常驻底条)
        /// </summary>
        private void DrawDataFlowLines(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int[] xOffsets = [7, 14];
            int[] widths = [2, 1];

            for (int lineIdx = 0; lineIdx < 2; lineIdx++) {
                int lx = rect.X + xOffsets[lineIdx];
                int lw = widths[lineIdx];
                float phase = dataLinePhases[lineIdx];
                int lineLen = (int)(rect.Height * 0.5f);
                int startY = rect.Y + (int)(phase * rect.Height);

                for (int dy = 0; dy < lineLen; dy++) {
                    int py = startY + dy;
                    if (py > rect.Bottom) py -= rect.Height;
                    if (py < rect.Y || py >= rect.Bottom) continue;

                    float t = dy / (float)lineLen;
                    float br = MathF.Sin(t * MathHelper.Pi) * 0.7f + 0.2f;
                    Color c = Color.Lerp(NeonBlue, DeepPurple, t * 0.7f)
                        * (alpha * br * 0.55f);
                    sb.Draw(px, new Rectangle(lx, py, lw, 1),
                        new Rectangle(0, 0, 1, 1), c);
                    sb.Draw(px, new Rectangle(lx - 1, py, 1, 1),
                        new Rectangle(0, 0, 1, 1), c * 0.18f);
                    sb.Draw(px, new Rectangle(lx + lw, py, 1, 1),
                        new Rectangle(0, 0, 1, 1), c * 0.18f);
                }
            }

            //左侧常驻底条
            sb.Draw(px, new Rectangle(rect.X + 4, rect.Y + 5, 1, rect.Height - 10),
                new Rectangle(0, 0, 1, 1), NeonBlueDim * (alpha * 0.18f));
        }

        /// <summary>
        /// 角括号装饰(CP2077式简洁L形角标 + 底部中心短横点缀)
        /// </summary>
        private void DrawCornerBrackets(SpriteBatch sb, Rectangle rect, float alpha, float hoverGlow) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float pulse = MathF.Sin(neonPulseTimer * 0.9f) * 0.1f + 0.9f;
            Color bc = NeonBlue * (alpha * (0.35f + hoverGlow * 0.3f) * pulse);
            int arm = 11;

            //左上L
            sb.Draw(px, new Rectangle(rect.X + 4, rect.Y + 4, 1, arm),
                new Rectangle(0, 0, 1, 1), bc);
            sb.Draw(px, new Rectangle(rect.X + 4, rect.Y + 4, arm, 1),
                new Rectangle(0, 0, 1, 1), bc);

            //右上L
            sb.Draw(px, new Rectangle(rect.Right - 5, rect.Y + 4, 1, arm),
                new Rectangle(0, 0, 1, 1), bc);
            sb.Draw(px, new Rectangle(rect.Right - 5 - arm, rect.Y + 4, arm, 1),
                new Rectangle(0, 0, 1, 1), bc);

            //左下L(略暗)
            sb.Draw(px, new Rectangle(rect.X + 4, rect.Bottom - 5 - arm, 1, arm),
                new Rectangle(0, 0, 1, 1), bc * 0.7f);
            sb.Draw(px, new Rectangle(rect.X + 4, rect.Bottom - 5, arm, 1),
                new Rectangle(0, 0, 1, 1), bc * 0.7f);

            //右下L(略暗)
            sb.Draw(px, new Rectangle(rect.Right - 5, rect.Bottom - 5 - arm, 1, arm),
                new Rectangle(0, 0, 1, 1), bc * 0.7f);
            sb.Draw(px, new Rectangle(rect.Right - 5 - arm, rect.Bottom - 5, arm, 1),
                new Rectangle(0, 0, 1, 1), bc * 0.7f);

            //底部中心 双短横线点缀
            int midX = rect.X + rect.Width / 2;
            sb.Draw(px, new Rectangle(midX - 16, rect.Bottom - 3, 12, 1),
                new Rectangle(0, 0, 1, 1), bc * 0.65f);
            sb.Draw(px, new Rectangle(midX + 4, rect.Bottom - 3, 12, 1),
                new Rectangle(0, 0, 1, 1), bc * 0.65f);
        }

        /// <summary>
        /// 四角状态文字(适配小面板:更小字号+更短文字)
        /// </summary>
        private void DrawCornerStatusText(SpriteBatch sb, Rectangle rect, float alpha) {
            if (alpha < 0.04f) return;
            float blink = MathF.Sin(neonPulseTimer * 0.7f) * 0.12f + 0.88f;
            Color col = NeonBlueDim * (alpha * 0.5f * blink);
            float sc = 0.45f;
            var font = FontAssets.MouseText.Value;

            //左上(贴近L角标内侧)
            Utils.DrawBorderString(sb, cornerStatus[0],
                new Vector2(rect.X + 19f, rect.Y + 5f), col, sc);
            //右上(右对齐)
            float w1 = font.MeasureString(cornerStatus[1]).X * sc;
            Utils.DrawBorderString(sb, cornerStatus[1],
                new Vector2(rect.Right - w1 - 18f, rect.Y + 5f), col, sc);
        }

        #endregion
    }
}
