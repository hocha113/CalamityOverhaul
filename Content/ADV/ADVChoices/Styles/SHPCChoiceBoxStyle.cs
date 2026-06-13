using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.ADVChoices.Styles
{
    /// <summary>
    /// SHPC 赛博选项样式
    /// </summary>
    internal class SHPCChoiceBoxStyle : IChoiceBoxStyle
    {
        // 动画计时器
        private float neonPulseTimer;
        private float dataFlowTimer;
        private float sweepTimer;

        // 左侧数据流线相位（2条，与对话框一致）
        private readonly float[] dataLinePhases = new float[2];

        // 四角状态文字
        private readonly string[] cornerStatus = ["LINK.OK", "SYS:RDY", "v2.07b", "SYNC.."];
        private int statusUpdateClock;
        private static readonly string[] StatusPool = [
            "MAID.OK", "SYS:RDY", "LINK.UP", "ACT:ON", "v2.07b",
            "NRG:98%", "SYNC..", "CORE:A+", "NET.OK", "STB:Hi",
            "IO:PASS", "CHK:OK", "MOD:RUN", "BUF:CLR", "SIG:99"
        ];

        // 粒子系统
        private readonly List<NeonMaidPRT> neonParticles = [];
        private int neonParticleSpawnTimer;
        private readonly List<CircuitNodePRT> circuitNodes = [];
        private int circuitNodeSpawnTimer;
        private const float SideMargin = 22f;

        // 六角溢出边距（shader控制alpha形状）
        private const int EdgePad = 16;

        // 主色调常量（与SHPCDialogueBox统一）
        private static readonly Color NeonBlue = new(60, 120, 255);
        private static readonly Color NeonBlueDim = new(40, 60, 180);
        private static readonly Color DeepPurple = new(100, 40, 200);
        private static readonly Color PanelDark = new(10, 6, 22);

        public void Update(Rectangle panelRect, bool active, bool closing) {
            Advance(ref neonPulseTimer, 0.028f);
            Advance(ref dataFlowTimer, 0.018f);
            sweepTimer += 0.004f;
            if (sweepTimer > 100f) sweepTimer -= 100f;

            // 左侧数据流线相位（2条）
            for (int i = 0; i < dataLinePhases.Length; i++)
                dataLinePhases[i] = (dataLinePhases[i] + 0.014f + i * 0.005f) % 1f;

            // 四角状态文字刷新（每50帧）
            statusUpdateClock++;
            if (statusUpdateClock >= 50) {
                statusUpdateClock = 0;
                for (int i = 0; i < cornerStatus.Length; i++)
                    cornerStatus[i] = StatusPool[Main.rand.Next(StatusPool.Length)];
            }

            // 霓虹粒子（间隔28帧，上限6个）
            float scaleW = Main.UIScale;
            neonParticleSpawnTimer++;
            if (active && !closing && neonParticleSpawnTimer >= 28 && neonParticles.Count < 6) {
                neonParticleSpawnTimer = 0;
                float left = panelRect.X + SideMargin * scaleW;
                float right = panelRect.Right - SideMargin * scaleW;
                Vector2 p = new(Main.rand.NextFloat(left, right),
                    panelRect.Y + Main.rand.NextFloat(20f, panelRect.Height - 20f));
                neonParticles.Add(new NeonMaidPRT(p));
            }
            for (int i = neonParticles.Count - 1; i >= 0; i--) {
                Vector2 panelPos = new(panelRect.X, panelRect.Y);
                Vector2 panelSize = new(panelRect.Width, panelRect.Height);
                if (neonParticles[i].Update(panelPos, panelSize))
                    neonParticles.RemoveAt(i);
            }

            // 电路节点（间隔38帧，上限3个）
            circuitNodeSpawnTimer++;
            if (active && !closing && circuitNodeSpawnTimer >= 38 && circuitNodes.Count < 3) {
                circuitNodeSpawnTimer = 0;
                float left = panelRect.X + SideMargin * scaleW;
                float right = panelRect.Right - SideMargin * scaleW;
                circuitNodes.Add(new CircuitNodePRT(
                    new Vector2(Main.rand.NextFloat(left, right),
                                panelRect.Y + Main.rand.NextFloat(20f, panelRect.Height - 20f))));
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

            // 1. 外阴影（紫色调，与对话框一致）
            for (int d = 6; d >= 1; d--) {
                Rectangle s = panelRect;
                s.Inflate(d, d);
                s.Offset(3, 4);
                spriteBatch.Draw(px, s, new Rectangle(0, 0, 1, 1),
                    new Color(6, 3, 12) * (alpha * 0.09f * (6f - d) / 6f));
            }

            // 2. 面板背景（Shader驱动蜂窝边框或降级程序化）
            DrawPanelBackground(spriteBatch, panelRect, alpha);

            // 3. 左侧数据流线
            DrawDataFlowLines(spriteBatch, panelRect, alpha);

            // 4. 角括号装饰
            DrawCornerBrackets(spriteBatch, panelRect, alpha);

            // 5. 状态文字
            DrawCornerStatusText(spriteBatch, panelRect, alpha);

            // 6. 粒子
            foreach (var node in circuitNodes)
                node.Draw(spriteBatch, alpha * 0.5f);
            foreach (var particle in neonParticles)
                particle.Draw(spriteBatch, alpha * 0.55f);
        }

        public void DrawChoiceBackground(SpriteBatch spriteBatch, Rectangle choiceRect, bool enabled, float hoverProgress, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            // 选项背景（深暗紫底色+悬停提亮）
            Color choiceBg = enabled
                ? Color.Lerp(PanelDark * 0.55f, new Color(18, 14, 42) * 0.75f, hoverProgress)
                : PanelDark * 0.25f;

            spriteBatch.Draw(px, choiceRect, new Rectangle(0, 0, 1, 1), choiceBg * alpha);

            // 内嵌暗边（上左暗+下右微亮）
            Color insetShadow = new Color(2, 2, 6) * (alpha * 0.35f);
            Color insetLight = NeonBlueDim * (alpha * 0.06f);
            spriteBatch.Draw(px, new Rectangle(choiceRect.X + 1, choiceRect.Y + 1, choiceRect.Width - 2, 1),
                new Rectangle(0, 0, 1, 1), insetShadow);
            spriteBatch.Draw(px, new Rectangle(choiceRect.X + 1, choiceRect.Y + 1, 1, choiceRect.Height - 2),
                new Rectangle(0, 0, 1, 1), insetShadow * 0.6f);
            spriteBatch.Draw(px, new Rectangle(choiceRect.X + 1, choiceRect.Bottom - 2, choiceRect.Width - 2, 1),
                new Rectangle(0, 0, 1, 1), insetLight);

            Color neonColor = GetEdgeColor(alpha);
            if (enabled && hoverProgress > 0.01f) {
                DrawChoiceBorder(spriteBatch, choiceRect, neonColor * (hoverProgress * 0.6f));
                DrawChoiceNeonIndicator(spriteBatch, choiceRect, neonColor, hoverProgress, alpha);
                DrawChoiceHoverGlow(spriteBatch, choiceRect, hoverProgress, alpha);
            }
            else if (enabled) {
                DrawChoiceBorder(spriteBatch, choiceRect, NeonBlueDim * (alpha * 0.15f));
            }
            else {
                DrawChoiceBorder(spriteBatch, choiceRect, new Color(25, 20, 50) * (alpha * 0.12f));
            }
        }

        public Color GetEdgeColor(float alpha) {
            float pulse = MathF.Sin(neonPulseTimer * 1.2f) * 0.2f + 0.8f;
            return Color.Lerp(NeonBlue, DeepPurple, 0.3f) * (alpha * 0.85f * pulse);
        }

        public Color GetTextGlowColor(float alpha, float hoverProgress) {
            return GetEdgeColor(alpha);
        }

        public void DrawTitleDecoration(SpriteBatch spriteBatch, Vector2 titlePos, string title, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            // 名字辉光晕（蓝紫色）
            Color nameGlow = NeonBlue * (alpha * 0.5f);
            for (int i = 0; i < 4; i++) {
                float a = MathHelper.TwoPi * i / 4f;
                Vector2 off = a.ToRotationVector2() * 1.8f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + off, nameGlow * 0.4f, 0.95f);
            }

            // 名字下方渐变细线（蓝→紫）
            float nameW = Terraria.GameContent.FontAssets.MouseText.Value
                .MeasureString(title).X * 0.95f;
            int lineW = (int)(nameW * 0.8f);
            for (int seg = 0; seg < lineW; seg++) {
                float t = seg / (float)lineW;
                float bright = MathF.Sin(t * MathHelper.Pi) * 0.7f + 0.3f;
                Color lineC = Color.Lerp(NeonBlue, DeepPurple, t * 0.6f)
                    * (alpha * 0.45f * bright);
                spriteBatch.Draw(px, new Rectangle((int)titlePos.X + seg, (int)(titlePos.Y + 20f), 1, 1),
                    new Rectangle(0, 0, 1, 1), lineC);
            }
        }

        public void DrawDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float len = end.X - start.X;
            if (len < 2f) return;

            // 单条蓝→紫渐变线（与对话框分割线一致）
            int segs = Math.Max(1, (int)(len / 6f));
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float segX = start.X + t * len;
                float segW = len / segs;
                float bright = MathF.Sin(t * MathHelper.Pi) * 0.6f + 0.4f;
                Color c = Color.Lerp(NeonBlue, DeepPurple, t)
                    * (alpha * 0.55f * bright);
                spriteBatch.Draw(px, new Rectangle((int)segX, (int)start.Y, (int)segW + 1, 1),
                    new Rectangle(0, 0, 1, 1), c);
            }
        }

        public void Reset() {
            neonPulseTimer = 0f;
            dataFlowTimer = 0f;
            sweepTimer = 0f;
            statusUpdateClock = 0;
            neonParticles.Clear();
            circuitNodes.Clear();
            neonParticleSpawnTimer = 0;
            circuitNodeSpawnTimer = 0;
            for (int i = 0; i < dataLinePhases.Length; i++)
                dataLinePhases[i] = 0f;
            cornerStatus[0] = "LINK.OK";
            cornerStatus[1] = "SYS:RDY";
            cornerStatus[2] = "v2.07b";
            cornerStatus[3] = "SYNC..";
        }

        #region 样式工具函数

        private static void Advance(ref float t, float speed) {
            t += speed;
            if (t > MathHelper.TwoPi) t -= MathHelper.TwoPi;
        }

        /// <summary>
        /// 面板背景：使用CyberPanel着色器（扩展矩形 + 蜂窝alpha遮罩），降级为程序化渐变
        /// </summary>
        private void DrawPanelBackground(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            if (EffectLoader.CyberPanel?.Value != null) {
                Effect effect = EffectLoader.CyberPanel.Value;

                Rectangle extRect = rect;
                extRect.Inflate(EdgePad, EdgePad);

                effect.Parameters["uTime"]?.SetValue(sweepTimer);
                effect.Parameters["uAlpha"]?.SetValue(alpha * 0.97f);
                effect.Parameters["uResolution"]?.SetValue(new Vector2(extRect.Width, extRect.Height));
                effect.Parameters["uEdgePad"]?.SetValue((float)EdgePad);

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, effect, Main.UIScaleMatrix);

                sb.Draw(px, extRect, Color.White);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, null, Main.UIScaleMatrix);
            }
            else {
                DrawFallbackBackground(sb, px, rect, alpha);
            }
        }

        /// <summary>
        /// 降级背景：渐变 + 扫描线 + 简易六角点阵 + 扫掠光带 + 暗角
        /// </summary>
        private void DrawFallbackBackground(SpriteBatch sb, Texture2D px, Rectangle rect, float alpha) {
            // 纯渐变背景（10段平滑，紫色调）
            int segs = 10;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Color c = Color.Lerp(new Color(16, 8, 28), new Color(8, 5, 20), t)
                    * (alpha * 0.97f);
                sb.Draw(px, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)),
                    new Rectangle(0, 0, 1, 1), c);
            }

            // 扫描线（每3px一条暗带）
            Color scanColor = new Color(20, 12, 45) * (alpha * 0.10f);
            for (int y = rect.Y; y < rect.Bottom; y += 3)
                sb.Draw(px, new Rectangle(rect.X + 4, y, rect.Width - 8, 1),
                    new Rectangle(0, 0, 1, 1), scanColor);

            // 简易六角点阵
            int dotSpacingX = 18;
            int dotSpacingY = 16;
            Color dotColor = new Color(40, 25, 80) * (alpha * 0.12f);
            for (int row = 0; row < rect.Height / dotSpacingY; row++) {
                int dy = rect.Y + row * dotSpacingY + 6;
                if (dy >= rect.Bottom - 4) continue;
                int offsetX = (row % 2 == 0) ? 0 : dotSpacingX / 2;
                for (int col = 0; col < rect.Width / dotSpacingX + 1; col++) {
                    int dx = rect.X + col * dotSpacingX + offsetX + 4;
                    if (dx >= rect.Right - 4) continue;
                    sb.Draw(px, new Rectangle(dx, dy, 1, 1),
                        new Rectangle(0, 0, 1, 1), dotColor);
                }
            }

            // 扫掠光带
            float scanY = rect.Y + (sweepTimer * 0.1f % 1f) * rect.Height;
            for (int dy = -4; dy <= 4; dy++) {
                int py = (int)scanY + dy;
                if (py < rect.Y || py >= rect.Bottom) continue;
                float fade = 1f - Math.Abs(dy) / 5f;
                sb.Draw(px, new Rectangle(rect.X + 4, py, rect.Width - 8, 1),
                    new Rectangle(0, 0, 1, 1), NeonBlueDim * (alpha * 0.12f * fade * fade));
            }

            // 暗角（左右两侧渐暗）
            int vigW = 16;
            for (int v = 0; v < vigW; v += 4) {
                float vFade = (1f - (float)v / vigW) * 0.1f;
                Color vColor = new Color(4, 2, 8) * (alpha * vFade);
                sb.Draw(px, new Rectangle(rect.X + v, rect.Y, 2, rect.Height),
                    new Rectangle(0, 0, 1, 1), vColor);
                sb.Draw(px, new Rectangle(rect.Right - v - 2, rect.Y, 2, rect.Height),
                    new Rectangle(0, 0, 1, 1), vColor);
            }
        }

        /// <summary>
        /// 左侧数据流线（2条竖向霓虹流动线 + 侧翼辉光 + 常驻底条）
        /// </summary>
        private void DrawDataFlowLines(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int[] xOffsets = [9, 18];
            int[] widths = [2, 2];

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
                    // 侧翼辉光
                    sb.Draw(px, new Rectangle(lx - 1, py, 1, 1),
                        new Rectangle(0, 0, 1, 1), c * 0.18f);
                    sb.Draw(px, new Rectangle(lx + lw, py, 1, 1),
                        new Rectangle(0, 0, 1, 1), c * 0.18f);
                }
            }

            // 左侧常驻底条
            sb.Draw(px, new Rectangle(rect.X + 5, rect.Y + 6, 1, rect.Height - 12),
                new Rectangle(0, 0, 1, 1), NeonBlueDim * (alpha * 0.18f));
        }

        /// <summary>
        /// 角括号装饰（CP2077式简洁L形角标 + 底部中心短横点缀）
        /// </summary>
        private void DrawCornerBrackets(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float pulse = MathF.Sin(neonPulseTimer * 0.9f) * 0.1f + 0.9f;
            Color bc = NeonBlue * (alpha * 0.3f * pulse);
            int arm = 12;

            // 右下角L括号
            sb.Draw(px, new Rectangle(rect.Right - 6, rect.Bottom - 6 - arm, 1, arm),
                new Rectangle(0, 0, 1, 1), bc);
            sb.Draw(px, new Rectangle(rect.Right - 6 - arm, rect.Bottom - 6, arm, 1),
                new Rectangle(0, 0, 1, 1), bc);

            // 底部中心双短横线
            int midX = rect.X + rect.Width / 2;
            sb.Draw(px, new Rectangle(midX - 16, rect.Bottom - 4, 12, 1),
                new Rectangle(0, 0, 1, 1), bc * 0.7f);
            sb.Draw(px, new Rectangle(midX + 4, rect.Bottom - 4, 12, 1),
                new Rectangle(0, 0, 1, 1), bc * 0.7f);
        }

        /// <summary>
        /// 四角状态文字（适配面板轮廓）
        /// </summary>
        private void DrawCornerStatusText(SpriteBatch sb, Rectangle rect, float alpha) {
            if (alpha < 0.04f) return;
            float blink = MathF.Sin(neonPulseTimer * 0.7f) * 0.12f + 0.88f;
            Color col = NeonBlueDim * (alpha * 0.4f * blink);
            float sc = 0.5f;
            var font = Terraria.GameContent.FontAssets.MouseText.Value;

            Utils.DrawBorderString(sb, cornerStatus[0],
                new Vector2(rect.X + 28f, rect.Y + 7f), col, sc);
            float w1 = font.MeasureString(cornerStatus[1]).X * sc;
            Utils.DrawBorderString(sb, cornerStatus[1],
                new Vector2(rect.Right - w1 - 16f, rect.Y + 7f), col, sc);
            Utils.DrawBorderString(sb, cornerStatus[2],
                new Vector2(rect.X + 8f, rect.Bottom - 16f), col * 0.6f, sc);
            float w3 = font.MeasureString(cornerStatus[3]).X * sc;
            Utils.DrawBorderString(sb, cornerStatus[3],
                new Vector2(rect.Right - w3 - 16f, rect.Bottom - 16f), col * 0.6f, sc);
        }

        /// <summary>
        /// 选项悬停时左侧霓虹竖线指示
        /// </summary>
        private void DrawChoiceNeonIndicator(SpriteBatch sb, Rectangle choiceRect, Color neonColor, float hoverProgress, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            const int dashH = 4, gapH = 3;
            float flow = dataFlowTimer * 20f;
            float period = dashH + gapH;
            Color dashColor = neonColor * (hoverProgress * 0.4f);

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

        /// <summary>
        /// 选项悬停时底部霓虹流光
        /// </summary>
        private void DrawChoiceHoverGlow(SpriteBatch sb, Rectangle choiceRect, float hoverProgress, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int glowW = (int)(choiceRect.Width * hoverProgress * 0.75f);
            // 主流光线（2px高）
            Color glowColor = NeonBlue * (alpha * hoverProgress * 0.35f);
            sb.Draw(px, new Rectangle(choiceRect.X + 1, choiceRect.Bottom - 2, glowW, 2),
                new Rectangle(0, 0, 1, 1), glowColor);
            // 上方模糊辉光扩散
            Color softGlow = NeonBlueDim * (alpha * hoverProgress * 0.12f);
            sb.Draw(px, new Rectangle(choiceRect.X + 1, choiceRect.Bottom - 4, glowW, 2),
                new Rectangle(0, 0, 1, 1), softGlow);
            // 流光头部亮点
            float pulse = MathF.Sin(neonPulseTimer * 3f) * 0.3f + 0.7f;
            int headX = choiceRect.X + 1 + glowW - 4;
            if (headX > choiceRect.X + 1 && headX < choiceRect.Right - 4) {
                Color headColor = new Color(100, 180, 255) * (alpha * hoverProgress * 0.5f * pulse);
                sb.Draw(px, new Rectangle(headX, choiceRect.Bottom - 3, 4, 3),
                    new Rectangle(0, 0, 1, 1), headColor);
            }
        }

        private static void DrawChoiceBorder(SpriteBatch sb, Rectangle rect, Color color) {
            Texture2D px = VaultAsset.placeholder2.Value;
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), color);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), color * 0.7f);
            int innerH = rect.Height - 2;
            if (innerH > 0) {
                sb.Draw(px, new Rectangle(rect.X, rect.Y + 1, 1, innerH),
                    new Rectangle(0, 0, 1, 1), color * 0.85f);
                sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y + 1, 1, innerH),
                    new Rectangle(0, 0, 1, 1), color * 0.85f);
            }
        }

        #endregion
    }
}
