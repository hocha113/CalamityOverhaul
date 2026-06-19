using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.DialogueBoxs.Styles
{
    /// <summary>SHPC 赛博朋克风格对话框</summary>
    internal class SHPCDialogueBox : DialogueBoxBase
    {
        public static SHPCDialogueBox Instance => UIHandleLoader.GetUIHandleOfType<SHPCDialogueBox>();
        public override string LocalizationCategory => "UI";

        private const float FixedWidth = 540f;
        protected override float PanelWidth => FixedWidth;

        //动画计时
        private float neonPulseTimer;      // 霓虹脉冲
        private float dataFlowTimer;       // 数据流
        private float sweepTimer;          // 扫掠光带

        //左侧数据流线
        private readonly float[] dataLinePhases = new float[2];

        //四角状态字
        private readonly string[] cornerStatus = ["LINK.OK", "SYS:RDY", "v2.07b", "SYNC.."];
        private int statusUpdateClock;
        private static readonly string[] StatusPool = [
            "MAID.OK", "SYS:RDY", "LINK.UP", "ACT:ON", "v2.07b",
            "NRG:98%", "SYNC..", "CORE:A+", "NET.OK", "STB:Hi",
            "IO:PASS", "CHK:OK", "MOD:RUN", "BUF:CLR", "SIG:99"
        ];

        //粒子
        private readonly List<NeonMaidPRT> neonParticles = [];
        private int neonParticleSpawnTimer;
        private readonly List<CircuitNodePRT> circuitNodes = [];
        private int circuitNodeSpawnTimer;
        private const float SideMargin = 24f;

        //六角边距，shader 控 alpha
        private const int EdgePad = 20;

        //主色调常量（深暗紫底色 + 蓝色高光）
        private static readonly Color NeonBlue = new(60, 120, 255);
        private static readonly Color NeonBlueDim = new(40, 60, 180);
        private static readonly Color DeepPurple = new(100, 40, 200);
        private static readonly Color PanelDark = new(10, 6, 22);

        #region 样式配置重写

        protected override float PortraitScaleMin => 0.88f;
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

        protected override Color GetSilhouetteColor(ContentDrawContext ctx)
            => new Color(12, 8, 24) * 0.9f;

        protected override Vector2 ApplyTextLineOffset(ContentDrawContext ctx, Vector2 basePosition, int lineIndex)
            => basePosition;

        protected override Color GetTextLineColor(ContentDrawContext ctx, int lineIndex)
            => Color.Lerp(new Color(200, 195, 240), Color.White, 0.15f) * ctx.ContentAlpha;

        protected override Color GetContinueHintColor(ContentDrawContext ctx, float blink)
            => NeonBlue * (blink * ctx.ContentAlpha * 0.8f);

        protected override Color GetFastHintColor(ContentDrawContext ctx)
            => NeonBlueDim * (0.4f * ctx.ContentAlpha);

        #endregion

        #region 模板方法实现

        /// <summary>头像框：深色底板 + 干净的双层霓虹边框</summary>
        protected override void DrawPortraitFrame(ContentDrawContext ctx, Rectangle frameRect) {
            SpriteBatch sb = ctx.SpriteBatch;
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = ctx.Alpha * ctx.PortraitData.Fade * ctx.PortraitExtraAlpha;
            float pulse = MathF.Sin(neonPulseTimer * 1.2f) * 0.15f + 0.85f;

            //深色底板
            sb.Draw(px, frameRect, new Rectangle(0, 0, 1, 1), PanelDark * (alpha * 0.95f));

            //主边框（蓝紫色 2px）
            Color borderColor = NeonBlue * (alpha * 0.7f * pulse);
            DrawRect(sb, px, frameRect, 2, borderColor);

            //外侧柔光（1px扩散）
            Rectangle outer = frameRect;
            outer.Inflate(2, 2);
            DrawRect(sb, px, outer, 1, NeonBlue * (alpha * 0.12f * pulse));
        }

        /// <summary>头像辉光：柔和的四边霓虹边缘光</summary>
        protected override void DrawPortraitGlow(ContentDrawContext ctx, Rectangle glowRect) {
            SpriteBatch sb = ctx.SpriteBatch;
            Texture2D px = VaultAsset.placeholder2.Value;
            float pulse = MathF.Sin(neonPulseTimer * 1.3f) * 0.2f + 0.8f;
            float fade = ctx.ContentAlpha * ctx.PortraitData.Fade * ctx.PortraitExtraAlpha;
            Color glow = NeonBlue * (fade * 0.25f * pulse);

            int w = 2;
            sb.Draw(px, new Rectangle(glowRect.X, glowRect.Y, glowRect.Width, w),
                new Rectangle(0, 0, 1, 1), glow);
            sb.Draw(px, new Rectangle(glowRect.X, glowRect.Bottom - w, glowRect.Width, w),
                new Rectangle(0, 0, 1, 1), glow * 0.6f);
            sb.Draw(px, new Rectangle(glowRect.X, glowRect.Y, w, glowRect.Height),
                new Rectangle(0, 0, 1, 1), glow * 0.8f);
            sb.Draw(px, new Rectangle(glowRect.Right - w, glowRect.Y, w, glowRect.Height),
                new Rectangle(0, 0, 1, 1), glow * 0.8f);
        }

        /// <summary>名称装饰：干净辉光 + 名字下方霓虹渐变细线</summary>
        protected override void DrawNameGlow(ContentDrawContext ctx, Vector2 position, float alpha) {
            SpriteBatch sb = ctx.SpriteBatch;
            Texture2D px = VaultAsset.placeholder2.Value;

            //名字辉光晕（蓝紫色）
            Color nameGlow = NeonBlue * (alpha * 0.5f);
            for (int i = 0; i < NameGlowCount; i++) {
                float a = MathHelper.TwoPi * i / NameGlowCount;
                Vector2 off = a.ToRotationVector2() * NameGlowRadius * ctx.SwitchEase;
                Utils.DrawBorderString(sb, current.Speaker, position + off, nameGlow * 0.4f, NameScale);
            }

            //名字下方渐变细线（蓝→紫）
            float nameW = Terraria.GameContent.FontAssets.MouseText.Value
                .MeasureString(current.Speaker).X * NameScale;
            int lineW = (int)(nameW * 0.8f);
            for (int seg = 0; seg < lineW; seg++) {
                float t = seg / (float)lineW;
                float bright = MathF.Sin(t * MathHelper.Pi) * 0.7f + 0.3f;
                Color lineC = Color.Lerp(NeonBlue, DeepPurple, t * 0.6f)
                    * (alpha * 0.45f * bright);
                sb.Draw(px, new Rectangle((int)position.X + seg, (int)(position.Y + 20f), 1, 1),
                    new Rectangle(0, 0, 1, 1), lineC);
            }
        }

        /// <summary>分割线：单条青→靛渐变线</summary>
        protected override void DrawDividerLine(ContentDrawContext ctx, Vector2 start, Vector2 end, float alpha) {
            SpriteBatch sb = ctx.SpriteBatch;
            Texture2D px = VaultAsset.placeholder2.Value;
            float len = end.X - start.X;
            if (len < 2f) return;

            int segs = Math.Max(1, (int)(len / 6f));
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float segX = start.X + t * len;
                float segW = len / segs;
                float bright = MathF.Sin(t * MathHelper.Pi) * 0.6f + 0.4f;
                Color c = Color.Lerp(NeonBlue, DeepPurple, t)
                    * (alpha * 0.55f * bright);
                sb.Draw(px, new Rectangle((int)segX, (int)start.Y, (int)segW + 1, 1),
                    new Rectangle(0, 0, 1, 1), c);
            }
        }

        #endregion

        protected override void StyleUpdate(Vector2 panelPos, Vector2 panelSize) {
            Advance(ref neonPulseTimer, 0.028f);
            Advance(ref dataFlowTimer, 0.018f);
            sweepTimer += 0.004f;
            if (sweepTimer > 100f) sweepTimer -= 100f;

            //数据流线相位（2条）
            for (int i = 0; i < dataLinePhases.Length; i++)
                dataLinePhases[i] = (dataLinePhases[i] + 0.014f + i * 0.005f) % 1f;

            //状态文字刷新（每55帧）
            statusUpdateClock++;
            if (statusUpdateClock >= 55) {
                statusUpdateClock = 0;
                for (int i = 0; i < cornerStatus.Length; i++)
                    cornerStatus[i] = StatusPool[Main.rand.Next(StatusPool.Length)];
            }

            //霓虹粒子（间隔28帧，上限8个）
            neonParticleSpawnTimer++;
            if (Active && neonParticleSpawnTimer >= 28 && neonParticles.Count < 8) {
                neonParticleSpawnTimer = 0;
                float scaleW = Main.UIScale;
                float left = panelPos.X + SideMargin * scaleW;
                float right = panelPos.X + panelSize.X - SideMargin * scaleW;
                neonParticles.Add(new NeonMaidPRT(
                    new Vector2(Main.rand.NextFloat(left, right),
                        panelPos.Y + Main.rand.NextFloat(30f, panelSize.Y - 30f))));
            }
            for (int i = neonParticles.Count - 1; i >= 0; i--) {
                if (neonParticles[i].Update(panelPos, panelSize))
                    neonParticles.RemoveAt(i);
            }

            //电路节点（间隔38帧，上限4个）
            circuitNodeSpawnTimer++;
            if (Active && circuitNodeSpawnTimer >= 38 && circuitNodes.Count < 4) {
                circuitNodeSpawnTimer = 0;
                float scaleW = Main.UIScale;
                float left = panelPos.X + SideMargin * scaleW;
                float right = panelPos.X + panelSize.X - SideMargin * scaleW;
                circuitNodes.Add(new CircuitNodePRT(
                    new Vector2(Main.rand.NextFloat(left, right),
                        panelPos.Y + Main.rand.NextFloat(30f, panelSize.Y - 30f))));
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
            Texture2D px = VaultAsset.placeholder2.Value;

            //1. 外阴影（精简为3层，紫色调）
            for (int d = 6; d >= 1; d--) {
                Rectangle s = panelRect;
                s.Inflate(d, d);
                s.Offset(3, 4);
                spriteBatch.Draw(px, s, new Rectangle(0, 0, 1, 1),
                    new Color(6, 3, 12) * (alpha * 0.09f * (6f - d) / 6f));
            }

            //2. 面板背景（Shader驱动蜂窝边框或降级程序化）
            DrawPanelBackground(spriteBatch, panelRect, alpha);

            //3. 左侧数据流线
            DrawDataFlowLines(spriteBatch, panelRect, alpha);

            //4. 角括号装饰
            DrawCornerBrackets(spriteBatch, panelRect, alpha);

            //5. 状态文字
            DrawCornerStatusText(spriteBatch, panelRect, alpha);

            //6. 粒子
            foreach (var node in circuitNodes)
                node.Draw(spriteBatch, alpha * 0.5f);
            foreach (var particle in neonParticles)
                particle.Draw(spriteBatch, alpha * 0.55f);

            DrawTimedProgressIndicator(spriteBatch, panelRect, alpha);

            if (current == null || contentAlpha <= 0.01f)
                return;

            DrawPortraitAndText(spriteBatch, panelRect, alpha, contentAlpha);
        }

        public override void ResetDialogueBox() {
            base.ResetDialogueBox();
            neonParticles.Clear();
            circuitNodes.Clear();
            neonPulseTimer = 0f;
            dataFlowTimer = 0f;
            sweepTimer = 0f;
        }

        #region 样式工具函数

        /// <summary>面板背景：使用CyberPanel着色器（扩展矩形 + 蜂窝alpha遮罩），降级为程序化渐变</summary>
        private void DrawPanelBackground(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            if (EffectLoader.CyberPanel?.Value != null) {
                Effect effect = EffectLoader.CyberPanel.Value;

                //扩展绘制区域，给六角溢出留空间
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

        /// <summary>降级背景：渐变 + 扫描线 + 简易六角点阵 + 面板分段线 + 扫掠光带 + 暗角</summary>
        private void DrawFallbackBackground(SpriteBatch sb, Texture2D px, Rectangle rect, float alpha) {
            //纯渐变背景（12段平滑，紫色调）
            int segs = 12;
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

            //扫描线（每3px一条暗带）
            Color scanColor = new Color(20, 12, 45) * (alpha * 0.10f);
            for (int y = rect.Y; y < rect.Bottom; y += 3)
                sb.Draw(px, new Rectangle(rect.X + 4, y, rect.Width - 8, 1),
                    new Rectangle(0, 0, 1, 1), scanColor);

            //简易六角点阵（用错行圆点模拟六角网格节点）
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

            //面板分段线（每55px一条水平暗沟+高光）
            Color seamDark = new Color(4, 3, 12) * (alpha * 0.3f);
            Color seamLight = new Color(35, 25, 80) * (alpha * 0.06f);
            for (int sy = rect.Y + 55; sy < rect.Bottom - 20; sy += 55) {
                sb.Draw(px, new Rectangle(rect.X + 6, sy, rect.Width - 12, 1),
                    new Rectangle(0, 0, 1, 1), seamDark);
                sb.Draw(px, new Rectangle(rect.X + 6, sy + 1, rect.Width - 12, 1),
                    new Rectangle(0, 0, 1, 1), seamLight);
            }

            //扫掠光带
            float scanY = rect.Y + (sweepTimer * 0.1f % 1f) * rect.Height;
            for (int dy = -4; dy <= 4; dy++) {
                int py = (int)scanY + dy;
                if (py < rect.Y || py >= rect.Bottom) continue;
                float fade = 1f - Math.Abs(dy) / 5f;
                sb.Draw(px, new Rectangle(rect.X + 4, py, rect.Width - 8, 1),
                    new Rectangle(0, 0, 1, 1), NeonBlueDim * (alpha * 0.12f * fade * fade));
            }

            //暗角（左右两侧渐暗）
            int vigW = 20;
            for (int v = 0; v < vigW; v += 4) {
                float vFade = (1f - (float)v / vigW) * 0.1f;
                Color vColor = new Color(4, 2, 8) * (alpha * vFade);
                sb.Draw(px, new Rectangle(rect.X + v, rect.Y, 2, rect.Height),
                    new Rectangle(0, 0, 1, 1), vColor);
                sb.Draw(px, new Rectangle(rect.Right - v - 2, rect.Y, 2, rect.Height),
                    new Rectangle(0, 0, 1, 1), vColor);
            }
        }

        /// <summary>左侧数据流线（2条竖向霓虹流动线 + 侧翼辉光 + 常驻底条）</summary>
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
                    sb.Draw(px, new Rectangle(lx - 1, py, 1, 1),
                        new Rectangle(0, 0, 1, 1), c * 0.18f);
                    sb.Draw(px, new Rectangle(lx + lw, py, 1, 1),
                        new Rectangle(0, 0, 1, 1), c * 0.18f);
                }
            }

            //左侧常驻底条
            sb.Draw(px, new Rectangle(rect.X + 5, rect.Y + 6, 1, rect.Height - 12),
                new Rectangle(0, 0, 1, 1), NeonBlueDim * (alpha * 0.18f));
        }

        /// <summary>角括号装饰（CP2077式简洁L形角标 + 底部中心短横点缀）</summary>
        private void DrawCornerBrackets(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float pulse = MathF.Sin(neonPulseTimer * 0.9f) * 0.1f + 0.9f;
            Color bc = NeonBlue * (alpha * 0.3f * pulse);
            int arm = 14;

            //右下角L括号
            sb.Draw(px, new Rectangle(rect.Right - 6, rect.Bottom - 6 - arm, 1, arm),
                new Rectangle(0, 0, 1, 1), bc);
            sb.Draw(px, new Rectangle(rect.Right - 6 - arm, rect.Bottom - 6, arm, 1),
                new Rectangle(0, 0, 1, 1), bc);

            //底部中心 双短横线
            int midX = rect.X + rect.Width / 2;
            sb.Draw(px, new Rectangle(midX - 20, rect.Bottom - 4, 16, 1),
                new Rectangle(0, 0, 1, 1), bc * 0.7f);
            sb.Draw(px, new Rectangle(midX + 4, rect.Bottom - 4, 16, 1),
                new Rectangle(0, 0, 1, 1), bc * 0.7f);
        }

        /// <summary>四角状态文字（适配斜切轮廓，右上/左下内缩避让）</summary>
        private void DrawCornerStatusText(SpriteBatch sb, Rectangle rect, float alpha) {
            if (alpha < 0.04f) return;
            float blink = MathF.Sin(neonPulseTimer * 0.7f) * 0.12f + 0.88f;
            Color col = NeonBlueDim * (alpha * 0.4f * blink);
            float sc = 0.5f;
            var font = Terraria.GameContent.FontAssets.MouseText.Value;

            //左上
            Utils.DrawBorderString(sb, cornerStatus[0],
                new Vector2(rect.X + 28f, rect.Y + 7f), col, sc);
            //右上
            float w1 = font.MeasureString(cornerStatus[1]).X * sc;
            Utils.DrawBorderString(sb, cornerStatus[1],
                new Vector2(rect.Right - w1 - 16f, rect.Y + 7f), col, sc);
            //左下
            Utils.DrawBorderString(sb, cornerStatus[2],
                new Vector2(rect.X + 8f, rect.Bottom - 16f), col * 0.6f, sc);
            //右下
            float w3 = font.MeasureString(cornerStatus[3]).X * sc;
            Utils.DrawBorderString(sb, cornerStatus[3],
                new Vector2(rect.Right - w3 - 16f, rect.Bottom - 16f), col * 0.6f, sc);
        }

        /// <summary>矩形线框（拐角不重叠）</summary>
        private static void DrawRect(SpriteBatch sb, Texture2D px, Rectangle r, int bw, Color c) {
            sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, bw), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(px, new Rectangle(r.X, r.Bottom - bw, r.Width, bw), new Rectangle(0, 0, 1, 1), c * 0.7f);
            int innerH = r.Height - bw * 2;
            if (innerH > 0) {
                sb.Draw(px, new Rectangle(r.X, r.Y + bw, bw, innerH), new Rectangle(0, 0, 1, 1), c * 0.85f);
                sb.Draw(px, new Rectangle(r.Right - bw, r.Y + bw, bw, innerH), new Rectangle(0, 0, 1, 1), c * 0.85f);
            }
        }

        #endregion
    }
}
