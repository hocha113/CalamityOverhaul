using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    /// <summary>
    /// 比目鱼UI的程序化矢量绘制层，全部基于1像素白纹理与参数化弧线步进
    /// 大面积氛围交给 HalibutPanel.fx / HalibutAtlasBg.fx，本类负责线、弧、环、辉光、文字等前景元素
    /// 设计参考 SHPCRenderer，但配色走 <see cref="HalibutTheme"/> 的深渊冷光体系
    /// </summary>
    internal static class HalibutRenderer
    {
        /// <summary>
        /// 1像素白纹理，所有矢量绘制的基础
        /// </summary>
        public static Texture2D Pixel => VaultAsset.placeholder2.Value;

        public static Vector2 AngleDir(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));

        #region 线段与圆弧
        /// <summary>
        /// 基础直线段
        /// </summary>
        public static void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 0.5f) {
                return;
            }
            sb.Draw(Pixel, start, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 带柔光的直线段，三层叠加模拟辉光
        /// </summary>
        public static void DrawGlowLine(SpriteBatch sb, Vector2 start, Vector2 end, float thickness, Color color) {
            DrawLine(sb, start, end, thickness + 3.5f, color * 0.16f);
            DrawLine(sb, start, end, thickness + 1.4f, color * 0.45f);
            DrawLine(sb, start, end, thickness, color);
        }

        /// <summary>
        /// 用密集径向线段绘制填充环形扇区，自适应分段保证无缝拼接
        /// </summary>
        public static void DrawArc(SpriteBatch sb, Vector2 center,
            float rIn, float rOut, float aStart, float aEnd, Color color) {
            if (aEnd <= aStart) {
                return;
            }
            float midR = (rIn + rOut) * 0.5f;
            float arcLen = (aEnd - aStart) * midR;
            int steps = Math.Max((int)(arcLen / 2.5f), 3);
            float aStep = (aEnd - aStart) / steps;
            float lineThick = MathF.Max(aStep * midR + 0.8f, 1.5f);
            for (int i = 0; i <= steps; i++) {
                float a = aStart + i * aStep;
                Vector2 dir = AngleDir(a);
                Vector2 p0 = center + dir * rIn;
                Vector2 p1 = center + dir * rOut;
                Vector2 diff = p1 - p0;
                float length = diff.Length();
                if (length < 0.5f) {
                    continue;
                }
                sb.Draw(Pixel, p0, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(),
                    new Vector2(0f, 0.5f), new Vector2(length, lineThick), SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 软边圆弧描边，三层不同厚度叠加模拟SDF抗锯齿
        /// </summary>
        public static void DrawArcStroke(SpriteBatch sb, Vector2 center,
            float radius, float aStart, float aEnd, float thickness, Color color) {
            if (aEnd <= aStart) {
                return;
            }
            DrawArc(sb, center, radius - thickness * 0.5f - 1.2f, radius + thickness * 0.5f + 1.2f,
                aStart, aEnd, color * 0.18f);
            DrawArc(sb, center, radius - thickness * 0.5f - 0.4f, radius + thickness * 0.5f + 0.4f,
                aStart, aEnd, color * 0.55f);
            DrawArc(sb, center, radius - thickness * 0.5f, radius + thickness * 0.5f,
                aStart, aEnd, color);
        }

        /// <summary>
        /// 程序化软边圆盘
        /// </summary>
        public static void DrawDisc(SpriteBatch sb, Vector2 center, float radius, float softPad, Color color) {
            if (radius <= 0f) {
                return;
            }
            DrawArc(sb, center, radius, radius + softPad, 0f, MathHelper.TwoPi, color * 0.25f);
            DrawArc(sb, center, MathF.Max(radius - 0.6f, 0f), radius + softPad * 0.5f, 0f, MathHelper.TwoPi, color * 0.55f);
            DrawArc(sb, center, 0f, radius, 0f, MathHelper.TwoPi, color);
        }

        /// <summary>
        /// 圆环描边
        /// </summary>
        public static void DrawRing(SpriteBatch sb, Vector2 center, float radius, float thickness, Color color) {
            DrawArcStroke(sb, center, radius, 0f, MathHelper.TwoPi, thickness, color);
        }

        /// <summary>
        /// 大范围径向柔光，多层圆盘衰减叠加，用于节点光晕与悬停辉光
        /// </summary>
        public static void DrawSoftGlow(SpriteBatch sb, Vector2 center, float radius, Color color) {
            for (int i = 0; i < 4; i++) {
                float t = i / 4f;
                float r = radius * (0.35f + t * 0.65f);
                DrawArc(sb, center, 0f, r, 0f, MathHelper.TwoPi, color * (0.22f * (1f - t)));
            }
        }

        /// <summary>
        /// 三次贝塞尔辉光曲线，用于连接丝线与飞行轨迹预览
        /// </summary>
        public static void DrawBezierGlow(SpriteBatch sb, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
            float thickness, Color color, int segments = 24) {
            Vector2 prev = p0;
            for (int i = 1; i <= segments; i++) {
                float t = i / (float)segments;
                Vector2 cur = CWRUtils.CubicBezier(t, p0, p1, p2, p3);
                DrawLine(sb, prev, cur, thickness + 2.4f, color * 0.18f);
                DrawLine(sb, prev, cur, thickness, color);
                prev = cur;
            }
        }

        /// <summary>
        /// 虚线
        /// </summary>
        public static void DrawDashedLine(SpriteBatch sb, Vector2 start, Vector2 end, Color color,
            float thickness, float dashLength, float gapLength, float offset = 0f) {
            Vector2 diff = end - start;
            float totalLength = diff.Length();
            if (totalLength < 0.5f) {
                return;
            }
            Vector2 dir = diff / totalLength;
            float pos = -offset % (dashLength + gapLength);
            while (pos < totalLength) {
                float dashStart = MathF.Max(0f, pos);
                float dashEnd = MathF.Min(totalLength, pos + dashLength);
                if (dashEnd > dashStart) {
                    DrawLine(sb, start + dir * dashStart, start + dir * dashEnd, thickness, color);
                }
                pos += dashLength + gapLength;
            }
        }

        /// <summary>
        /// 流动虚线警告边框
        /// </summary>
        public static void DrawDashedRectBorder(SpriteBatch sb, Rectangle rect, Color color,
            float thickness, float dashLength, float gapLength, float flowOffset) {
            DrawDashedLine(sb, new Vector2(rect.Left, rect.Top), new Vector2(rect.Right, rect.Top),
                color, thickness, dashLength, gapLength, flowOffset);
            DrawDashedLine(sb, new Vector2(rect.Right, rect.Bottom), new Vector2(rect.Left, rect.Bottom),
                color, thickness, dashLength, gapLength, flowOffset);
            DrawDashedLine(sb, new Vector2(rect.Left, rect.Bottom), new Vector2(rect.Left, rect.Top),
                color, thickness, dashLength, gapLength, flowOffset);
            DrawDashedLine(sb, new Vector2(rect.Right, rect.Top), new Vector2(rect.Right, rect.Bottom),
                color, thickness, dashLength, gapLength, flowOffset);
        }
        #endregion

        #region 装饰元素
        /// <summary>
        /// 八芒星装饰
        /// </summary>
        public static void DrawStar(SpriteBatch sb, Vector2 position, float size, Color color) {
            sb.Draw(Pixel, position, new Rectangle(0, 0, 1, 1), color,
                0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0);
            sb.Draw(Pixel, position, new Rectangle(0, 0, 1, 1), color,
                MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0);
            sb.Draw(Pixel, position, new Rectangle(0, 0, 1, 1), color * 0.7f,
                MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.7f, size * 0.2f), SpriteEffects.None, 0);
            sb.Draw(Pixel, position, new Rectangle(0, 0, 1, 1), color * 0.7f,
                -MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.7f, size * 0.2f), SpriteEffects.None, 0);
        }

        /// <summary>
        /// 渐变线条，用于分割线
        /// </summary>
        public static void DrawGradientLine(SpriteBatch sb, Vector2 start, Vector2 end,
            Color startColor, Color endColor, float thickness) {
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }
            edge /= length;
            int segments = Math.Max(1, (int)(length / 10f));
            for (int i = 0; i < segments; i++) {
                float t = (float)i / segments;
                Vector2 segPos = start + edge * (length * t);
                Color color = Color.Lerp(startColor, endColor, t);
                DrawLine(sb, segPos, segPos + edge * (length / segments), thickness, color);
            }
        }

        /// <summary>
        /// 冷却扫掠遮罩：在图标区域上从顶部顺时针盖一层暗色扇区，ratio=剩余冷却比例
        /// </summary>
        public static void DrawCooldownSweep(SpriteBatch sb, Vector2 center, float radius, float ratio, float alpha) {
            ratio = MathHelper.Clamp(ratio, 0f, 1f);
            if (ratio <= 0.005f) {
                return;
            }
            float aStart = -MathHelper.PiOver2;
            float aEnd = aStart + MathHelper.TwoPi * ratio;
            DrawArc(sb, center, 0f, radius, aStart, aEnd, HalibutTheme.Void * (0.62f * alpha));
            //扫掠前沿亮线
            Vector2 dir = AngleDir(aEnd);
            DrawLine(sb, center, center + dir * radius, 1.4f, HalibutTheme.GlowHi * (0.5f * alpha));
        }

        /// <summary>
        /// 程序化眼睛：上下眼睑弧 + 虹膜 + 瞳孔 + 辉光
        /// </summary>
        /// <param name="sb">画布</param>
        /// <param name="center">眼睛中心</param>
        /// <param name="size">半宽（眼角到中心的距离）</param>
        /// <param name="openAmount">睁眼程度 0闭-1全开</param>
        /// <param name="irisColor">虹膜颜色</param>
        /// <param name="alpha">整体透明度</param>
        /// <param name="time">动画时间（用于虹膜内部游动）</param>
        public static void DrawEye(SpriteBatch sb, Vector2 center, float size,
            float openAmount, Color irisColor, float alpha, float time) {
            openAmount = MathHelper.Clamp(openAmount, 0f, 1f);
            float h = size * 0.62f * MathF.Max(openAmount, 0.06f);
            Color lidColor = Color.Lerp(HalibutTheme.TextDim, HalibutTheme.GlowHi, openAmount * 0.6f) * alpha;

            //上下眼睑：用两段对称二次贝塞尔折线近似
            int segs = 14;
            Vector2 left = center + new Vector2(-size, 0f);
            Vector2 right = center + new Vector2(size, 0f);
            Vector2 prevTop = left;
            Vector2 prevBot = left;
            for (int i = 1; i <= segs; i++) {
                float t = i / (float)segs;
                float x = MathHelper.Lerp(-size, size, t);
                float arch = MathF.Sin(t * MathHelper.Pi);
                Vector2 top = center + new Vector2(x, -arch * h);
                Vector2 bot = center + new Vector2(x, arch * h);
                DrawLine(sb, prevTop, top, 1.6f, lidColor);
                DrawLine(sb, prevBot, bot, 1.4f, lidColor * 0.8f);
                prevTop = top;
                prevBot = bot;
            }

            if (openAmount < 0.1f) {
                return;//闭眼时不画虹膜
            }

            //虹膜与瞳孔，虹膜带轻微游动
            float irisR = size * 0.42f * openAmount;
            Vector2 drift = new(MathF.Sin(time * 0.8f + center.X * 0.05f) * size * 0.07f, 0f);
            Vector2 irisPos = center + drift;
            DrawSoftGlow(sb, irisPos, irisR * 2.2f, irisColor * (alpha * 0.5f));
            DrawDisc(sb, irisPos, irisR, 2f, irisColor * alpha);
            DrawDisc(sb, irisPos, irisR * 0.42f, 1.2f, HalibutTheme.Void * alpha);
            //高光点
            DrawDisc(sb, irisPos + new Vector2(-irisR * 0.3f, -irisR * 0.34f), irisR * 0.16f, 0.8f,
                HalibutTheme.Caustic * (alpha * 0.9f));
        }
        #endregion

        #region 文字
        /// <summary>
        /// 四向辉光描边文字
        /// </summary>
        public static void DrawGlowText(SpriteBatch sb, string text, Vector2 pos,
            Color textColor, Color glowColor, float scale, float glowRadius = 1.3f) {
            if (string.IsNullOrEmpty(text)) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f;
                Vector2 offset = AngleDir(ang) * glowRadius;
                Utils.DrawBorderString(sb, text, pos + offset, glowColor, scale);
            }
            Utils.DrawBorderString(sb, text, pos, textColor, scale);
        }

        /// <summary>
        /// 居中绘制辉光文字，返回测量尺寸
        /// </summary>
        public static Vector2 DrawGlowTextCentered(SpriteBatch sb, string text, Vector2 center,
            Color textColor, Color glowColor, float scale, float glowRadius = 1.3f) {
            if (string.IsNullOrEmpty(text)) {
                return Vector2.Zero;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 size = font.MeasureString(text) * scale;
            DrawGlowText(sb, text, center - size * 0.5f, textColor, glowColor, scale, glowRadius);
            return size;
        }
        #endregion

        #region 着色器面板
        /// <summary>
        /// 用 HalibutPanel.fx 绘制深海面板背板；着色器缺失时回退为CPU暗色圆角面板
        /// </summary>
        /// <param name="sb">画布</param>
        /// <param name="rect">面板屏幕区域</param>
        /// <param name="alpha">整体透明度</param>
        /// <param name="depth">深度氛围 0浅-1深，驱动基调明暗</param>
        /// <param name="agitation">躁动度 0-1，驱动深渊红色不安</param>
        /// <param name="contentDim">中央内容区压暗程度 0-1，文字多时调大保证可读性</param>
        public static void DrawSeaPanel(SpriteBatch sb, Rectangle rect, float alpha,
            float depth = 0.5f, float agitation = 0f, float contentDim = 0.5f) {
            if (rect.Width < 4 || rect.Height < 4 || alpha < 0.01f) {
                return;
            }
            Effect effect = EffectLoader.HalibutPanel?.Value;
            if (effect == null) {
                DrawSeaPanel_CPU(sb, rect, alpha);
                return;
            }
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(rect.Width, rect.Height));
            effect.Parameters["uDepth"]?.SetValue(MathHelper.Clamp(depth, 0f, 1f));
            effect.Parameters["uAgitation"]?.SetValue(MathHelper.Clamp(agitation, 0f, 1f));
            effect.Parameters["uContentDim"]?.SetValue(MathHelper.Clamp(contentDim, 0f, 1f));
            ShaderQuad(sb, effect, rect);
        }

        private static void DrawSeaPanel_CPU(SpriteBatch sb, Rectangle rect, float alpha) {
            Rectangle shadow = rect;
            shadow.Offset(3, 4);
            sb.Draw(Pixel, shadow, new Rectangle(0, 0, 1, 1), Color.Black * (0.45f * alpha));
            sb.Draw(Pixel, rect, new Rectangle(0, 0, 1, 1), HalibutTheme.PanelBg * (0.94f * alpha));
            Color edge = HalibutTheme.Glow * (0.55f * alpha);
            DrawLine(sb, new Vector2(rect.Left, rect.Top), new Vector2(rect.Right, rect.Top), 1.4f, edge);
            DrawLine(sb, new Vector2(rect.Left, rect.Bottom), new Vector2(rect.Right, rect.Bottom), 1.4f, edge * 0.7f);
            DrawLine(sb, new Vector2(rect.Left, rect.Top), new Vector2(rect.Left, rect.Bottom), 1.4f, edge * 0.85f);
            DrawLine(sb, new Vector2(rect.Right, rect.Top), new Vector2(rect.Right, rect.Bottom), 1.4f, edge * 0.85f);
        }

        /// <summary>
        /// 用 HalibutAtlasBg.fx 绘制图鉴海域背景；着色器缺失时回退为纵向渐变
        /// </summary>
        /// <param name="sb">画布</param>
        /// <param name="rect">覆盖区域（一般近全屏）</param>
        /// <param name="alpha">整体透明度</param>
        /// <param name="depth">当前下潜深度 0海面-1渊底</param>
        /// <param name="agitation">深渊躁动（复苏比例驱动）</param>
        /// <param name="scrollPx">当前滚动像素值，驱动背景视差</param>
        public static void DrawAtlasBackground(SpriteBatch sb, Rectangle rect, float alpha,
            float depth, float agitation, float scrollPx) {
            if (rect.Width < 4 || rect.Height < 4 || alpha < 0.01f) {
                return;
            }
            Effect effect = EffectLoader.HalibutAtlasBg?.Value;
            if (effect == null) {
                //CPU回退：简单的深度渐变
                int bands = 24;
                for (int i = 0; i < bands; i++) {
                    float t = i / (float)bands;
                    int y0 = rect.Y + (int)(t * rect.Height);
                    int y1 = rect.Y + (int)((i + 1) / (float)bands * rect.Height);
                    Color c = Color.Lerp(HalibutTheme.Mid, HalibutTheme.Void, MathHelper.Clamp(t + depth * 0.5f, 0f, 1f));
                    sb.Draw(Pixel, new Rectangle(rect.X, y0, rect.Width, Math.Max(1, y1 - y0)),
                        new Rectangle(0, 0, 1, 1), c * (0.93f * alpha));
                }
                return;
            }
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(rect.Width, rect.Height));
            effect.Parameters["uDepth"]?.SetValue(MathHelper.Clamp(depth, 0f, 1f));
            effect.Parameters["uAgitation"]?.SetValue(MathHelper.Clamp(agitation, 0f, 1f));
            effect.Parameters["uScroll"]?.SetValue(scrollPx);
            ShaderQuad(sb, effect, rect);
        }

        /// <summary>
        /// 着色器全参数四边形绘制：切换到Immediate模式应用效果后恢复Deferred
        /// </summary>
        private static void ShaderQuad(SpriteBatch sb, Effect effect, Rectangle dest) {
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            sb.Draw(Pixel, dest, Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }
        #endregion

        #region 光标信息面板
        /// <summary>
        /// 统一的光标悬浮信息面板：自动测量换行、屏幕边缘自适应、深海背板
        /// 取代旧UI中三份重复的tooltip布局实现
        /// </summary>
        /// <param name="sb">画布</param>
        /// <param name="cursor">光标位置</param>
        /// <param name="title">标题（可空）</param>
        /// <param name="titleColor">标题颜色</param>
        /// <param name="body">正文（自动换行，可空）</param>
        /// <param name="alpha">整体透明度</param>
        /// <param name="rightTag">右上角小标签（可空，比如“已死机”）</param>
        /// <param name="rightTagColor">标签颜色</param>
        /// <param name="minWidth">最小宽度</param>
        /// <param name="maxWidth">最大宽度</param>
        public static void DrawCursorPanel(SpriteBatch sb, Vector2 cursor, string title, Color titleColor,
            string body, float alpha, string rightTag = null, Color rightTagColor = default,
            float minWidth = 230f, float maxWidth = 400f) {
            if (alpha < 0.02f) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            const float pad = 13f;
            const float titleScale = 0.86f;
            const float bodyScale = 0.74f;
            const float lineH = 18f;

            float contentWidth = minWidth - pad * 2f;
            string[] lines = string.IsNullOrEmpty(body)
                ? []
                : Utils.WordwrapString(body, font, (int)(contentWidth + 40), 30, out _);

            //测量最长行，必要时拓宽并重新换行
            float longest = 0f;
            foreach (string l in lines) {
                if (string.IsNullOrWhiteSpace(l)) {
                    continue;
                }
                longest = MathF.Max(longest, font.MeasureString(l.TrimEnd('-', ' ')).X * bodyScale);
            }
            float titleW = string.IsNullOrEmpty(title) ? 0f : font.MeasureString(title).X * titleScale;
            if (!string.IsNullOrEmpty(rightTag)) {
                titleW += font.MeasureString(rightTag).X * 0.62f + 26f;
            }
            longest = MathF.Max(longest, titleW);
            if (longest > contentWidth) {
                contentWidth = MathHelper.Clamp(longest, contentWidth, maxWidth - pad * 2f);
                lines = string.IsNullOrEmpty(body)
                    ? []
                    : Utils.WordwrapString(body, font, (int)(contentWidth / bodyScale * 0.95f + 40), 30, out _);
            }

            int drawLines = 0;
            foreach (string l in lines) {
                if (!string.IsNullOrWhiteSpace(l)) {
                    drawLines++;
                }
            }

            float titleHeight = string.IsNullOrEmpty(title) ? 0f : font.MeasureString(title).Y * titleScale + 7f;
            float dividerBlock = string.IsNullOrEmpty(title) || drawLines == 0 ? 0f : 9f;
            float panelW = contentWidth + pad * 2f;
            float panelH = pad * 1.6f + titleHeight + dividerBlock + drawLines * lineH;

            Vector2 panelPos = cursor + new Vector2(18f, -panelH - 10f);
            panelPos.X = MathHelper.Clamp(panelPos.X, 8f, Main.screenWidth - panelW - 8f);
            panelPos.Y = MathHelper.Clamp(panelPos.Y, 8f, Main.screenHeight - panelH - 8f);
            Rectangle rect = new((int)panelPos.X, (int)panelPos.Y, (int)panelW, (int)panelH);

            DrawSeaPanel(sb, rect, alpha, 0.6f, 0f, 0.6f);

            float y = rect.Y + pad * 0.7f;
            if (!string.IsNullOrEmpty(title)) {
                DrawGlowText(sb, title, new Vector2(rect.X + pad, y),
                    titleColor * alpha, titleColor * (alpha * 0.35f), titleScale);
                if (!string.IsNullOrEmpty(rightTag)) {
                    Vector2 tagSize = font.MeasureString(rightTag) * 0.62f;
                    DrawGlowText(sb, rightTag, new Vector2(rect.Right - pad - tagSize.X, y + 2f),
                        rightTagColor * alpha, rightTagColor * (alpha * 0.4f), 0.62f, 1.1f);
                }
                y += titleHeight;
                if (drawLines > 0) {
                    DrawGradientLine(sb, new Vector2(rect.X + pad, y), new Vector2(rect.Right - pad, y),
                        titleColor * (alpha * 0.75f), titleColor * (alpha * 0.06f), 1.2f);
                    y += dividerBlock;
                }
            }
            foreach (string raw in lines) {
                if (string.IsNullOrWhiteSpace(raw)) {
                    continue;
                }
                string line = raw.TrimEnd('-', ' ');
                Utils.DrawBorderString(sb, line, new Vector2(rect.X + pad + 1f, y + 1f), Color.Black * (alpha * 0.5f), bodyScale);
                Utils.DrawBorderString(sb, line, new Vector2(rect.X + pad, y), HalibutTheme.Text * alpha, bodyScale);
                y += lineH;
            }

            //角落星芒点缀
            float starTime = Main.GlobalTimeWrappedHourly * 3f;
            float s1 = (MathF.Sin(starTime) * 0.5f + 0.5f) * alpha;
            DrawStar(sb, new Vector2(rect.Right - 13, rect.Y + 11), 3.6f, HalibutTheme.Glow * s1);
        }
        #endregion
    }
}
