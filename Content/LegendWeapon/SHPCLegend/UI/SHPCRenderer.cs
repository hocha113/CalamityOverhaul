using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.RAMSystems;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI
{
    /// <summary>SHPC 扇形 HUD CPU 绘制，径向偏移+alpha 伪 SDF 软边</summary>
    internal static class SHPCRenderer
    {
        #region 基础几何工具

        public static Vector2 AngleDir(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));

        public static float EaseOutCubic(float t) {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        public static float EaseInOutQuad(float t) =>
            t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) * 0.5f;

        public static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float inv = t - 1f;
            return 1f + c3 * inv * inv * inv + c1 * inv * inv;
        }

        #endregion

        #region 线段与圆弧

        /// <summary>直线段，start锚点旋转拉伸1px纹理</summary>
        public static void DrawLine(SpriteBatch sb, Texture2D px, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 0.5f) {
                return;
            }
            sb.Draw(px, start, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(),
                Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        /// <summary>径向线段填弧，自适应分段</summary>
        public static void DrawArc(SpriteBatch sb, Texture2D px, Vector2 center,
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
                DrawLine(sb, px, center + dir * rIn, center + dir * rOut, lineThick, color);
            }
        }

        /// <summary>软边圆弧描边，三层厚度伪抗锯齿</summary>
        public static void DrawArcStroke(SpriteBatch sb, Texture2D px, Vector2 center,
            float radius, float aStart, float aEnd, float thickness, Color color) {
            if (aEnd <= aStart) {
                return;
            }
            //外层柔光
            DrawArc(sb, px, center,
                radius - thickness * 0.5f - 1.2f,
                radius + thickness * 0.5f + 1.2f,
                aStart, aEnd, color * 0.18f);
            //中层主体
            DrawArc(sb, px, center,
                radius - thickness * 0.5f - 0.4f,
                radius + thickness * 0.5f + 0.4f,
                aStart, aEnd, color * 0.55f);
            //内层锐线
            DrawArc(sb, px, center,
                radius - thickness * 0.5f,
                radius + thickness * 0.5f,
                aStart, aEnd, color);
        }

        /// <summary>软边圆盘，radius核心，softPad过渡</summary>
        public static void DrawDisc(SpriteBatch sb, Texture2D px, Vector2 center,
            float radius, float softPad, Color color) {
            if (radius <= 0f) {
                return;
            }
            //外层
            DrawArc(sb, px, center, radius, radius + softPad, 0f, MathHelper.TwoPi, color * 0.25f);
            //过渡
            DrawArc(sb, px, center, radius - 0.6f, radius + softPad * 0.5f, 0f, MathHelper.TwoPi, color * 0.55f);
            //核心填充
            DrawArc(sb, px, center, 0f, radius, 0f, MathHelper.TwoPi, color);
        }

        /// <summary>圆环描边，三层软边</summary>
        public static void DrawRing(SpriteBatch sb, Texture2D px, Vector2 center,
            float radius, float thickness, Color color) {
            DrawArcStroke(sb, px, center, radius, 0f, MathHelper.TwoPi, thickness, color);
        }

        #endregion

        #region 核心绘制

        /// <summary>左下能量核心，优先SHPCCoreOrb，降级CPU</summary>
        public static void DrawCore(SpriteBatch sb, Texture2D px, Vector2 center,
            float expandProgress, float coreHover, float corePulse, float clickFlash, float time, float globalAlpha) {
            Effect effect = EffectLoader.SHPCCoreOrb?.Value;
            if (effect != null) {
                DrawCore_Shader(sb, px, center, expandProgress, coreHover, corePulse,
                    clickFlash, time, globalAlpha, effect);
            }
            else {
                DrawCore_CPU(sb, px, center, expandProgress, coreHover, corePulse,
                    clickFlash, time, globalAlpha);
            }
        }

        private static void DrawCore_Shader(SpriteBatch sb, Texture2D px, Vector2 center,
            float expandProgress, float coreHover, float corePulse, float clickFlash,
            float time, float globalAlpha, Effect effect) {
            //包围盒，外环/刻度/冲击波/辉光
            const float pad = 18f;
            float boxR = SHPCTheme.CoreRingR + 30f + pad;
            float qLeft = MathF.Max(0f, center.X - boxR);
            float qTop = MathF.Max(0f, center.Y - boxR);
            float qRight = MathF.Min(Main.screenWidth, center.X + boxR);
            float qBottom = MathF.Min(Main.screenHeight, center.Y + boxR);
            int qW = (int)MathF.Ceiling(qRight - qLeft);
            int qH = (int)MathF.Ceiling(qBottom - qTop);
            if (qW <= 0 || qH <= 0) {
                return;
            }
            Rectangle dest = new((int)qLeft, (int)qTop, qW, qH);
            Vector2 relCenter = new(center.X - qLeft, center.Y - qTop);

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(globalAlpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(qW, qH));
            effect.Parameters["uCenter"]?.SetValue(relCenter);
            effect.Parameters["uCoreRingR"]?.SetValue(SHPCTheme.CoreRingR);
            effect.Parameters["uCoreRadius"]?.SetValue(SHPCTheme.CoreRadius);
            effect.Parameters["uExpand"]?.SetValue(MathHelper.Clamp(expandProgress, 0f, 1f));
            effect.Parameters["uHover"]?.SetValue(MathHelper.Clamp(coreHover, 0f, 1f));
            effect.Parameters["uPulse"]?.SetValue(MathHelper.Clamp(corePulse, 0f, 1f));
            effect.Parameters["uClickFlash"]?.SetValue(MathHelper.Clamp(clickFlash, 0f, 1f));

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            sb.Draw(px, dest, Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        private static void DrawCore_CPU(SpriteBatch sb, Texture2D px, Vector2 center,
            float expandProgress, float coreHover, float corePulse, float clickFlash,
            float time, float globalAlpha) {
            //外层投影，给整体一种漂浮感
            DrawDisc(sb, px, center + new Vector2(0f, 2.5f),
                SHPCTheme.CoreRingR + 4f, 6f, SHPCTheme.ShadowDark * (0.45f * globalAlpha));

            //背景圆盘，深色底
            DrawDisc(sb, px, center,
                SHPCTheme.CoreRingR - 1f, 3f, SHPCTheme.SlotBg * (0.92f * globalAlpha));

            //外环，展开时随展开进度逐渐加亮
            float ringGlow = 0.55f + expandProgress * 0.35f + coreHover * 0.25f + clickFlash * 0.5f;
            ringGlow = MathHelper.Clamp(ringGlow, 0f, 1.4f);
            Color ringCol = Color.Lerp(SHPCTheme.Cyan, SHPCTheme.CyanHi, ringGlow * 0.6f);
            DrawArcStroke(sb, px, center, SHPCTheme.CoreRingR, 0f, MathHelper.TwoPi,
                1.6f, ringCol * (ringGlow * globalAlpha));

            //旋转刻度，4个短弧表达"扇区指引"
            int markCount = 4;
            float markSpan = 0.42f;
            float markGap = MathHelper.TwoPi / markCount;
            float markRot = time * 0.35f;
            for (int i = 0; i < markCount; i++) {
                float a0 = markRot + i * markGap - markSpan * 0.5f;
                DrawArcStroke(sb, px, center, SHPCTheme.CoreRingR + 4f, a0, a0 + markSpan,
                    1.1f, SHPCTheme.Border * (0.6f * globalAlpha));
            }

            //内核呼吸光斑
            float breath = 0.78f + MathF.Sin(time * 2.4f) * 0.15f + corePulse * 0.4f;
            DrawDisc(sb, px, center,
                SHPCTheme.CoreRadius * breath * 0.5f, 4f,
                SHPCTheme.CyanHi * (0.85f * globalAlpha));

            //点击瞬闪扩散环
            if (clickFlash > 0.01f) {
                float flashR = SHPCTheme.CoreRingR + (1f - clickFlash) * 30f;
                DrawArcStroke(sb, px, center, flashR, 0f, MathHelper.TwoPi,
                    2.2f, SHPCTheme.CyanHi * (clickFlash * 0.85f * globalAlpha));
            }

            //中央十字微元素，纯粹增加构图细节
            DrawLine(sb, px, center - new Vector2(3f, 0f), center + new Vector2(3f, 0f),
                1.1f, SHPCTheme.SlotBg * globalAlpha);
            DrawLine(sb, px, center - new Vector2(0f, 3f), center + new Vector2(0f, 3f),
                1.1f, SHPCTheme.SlotBg * globalAlpha);
        }

        #endregion

        #region 扇形按钮

        /// <summary>扇形按钮，expandProgress入场/透明度</summary>
        public static void DrawSector(SpriteBatch sb, Texture2D px, Vector2 center,
            float aStart, float aEnd, float expandProgress,
            float hoverAmt, float selectAmt, bool enabled, float statusValue,
            string glyph, float time, float globalAlpha) {
            if (expandProgress < 0.01f) {
                return;
            }

            //入场从核心滑出
            float ease = EaseOutBack(MathHelper.Clamp(expandProgress, 0f, 1f));
            float rIn = MathHelper.Lerp(SHPCTheme.CoreRingR + 4f, SHPCTheme.ButtonInnerR, ease);
            float rOut = MathHelper.Lerp(SHPCTheme.CoreRingR + 8f, SHPCTheme.ButtonOuterR, ease);
            float a = globalAlpha * MathHelper.Clamp(expandProgress * 1.3f, 0f, 1f);

            //投影
            DrawArc(sb, px, center + new Vector2(1.5f, 2.5f),
                rIn, rOut, aStart, aEnd, SHPCTheme.ShadowDark * (0.55f * a));

            //底板，禁用时整体偏暗
            Color bgCol = enabled ? SHPCTheme.SlotBg : SHPCTheme.SlotBg * 0.55f;
            DrawArc(sb, px, center, rIn + 1f, rOut - 1f, aStart, aEnd, bgCol * (0.92f * a));

            //内侧暗化条
            DrawArc(sb, px, center, rIn + 1f, rIn + 4f, aStart, aEnd,
                SHPCTheme.ShadowDark * (0.55f * a));

            //状态弧填充，按状态值从内到外按比例覆盖
            if (enabled && statusValue > 0.01f) {
                float fillR = MathHelper.Lerp(rIn + 1f, rOut - 1f, MathHelper.Clamp(statusValue, 0f, 1f));
                Color fillCol = Color.Lerp(SHPCTheme.Cyan, SHPCTheme.CyanHi, hoverAmt * 0.6f + selectAmt * 0.5f);
                //主填充
                DrawArc(sb, px, center, rIn + 1f, fillR, aStart, aEnd, fillCol * (0.55f * a));
                //外缘高光
                DrawArc(sb, px, center, fillR - 1.5f, fillR + 1f, aStart, aEnd, SHPCTheme.CyanHi * (0.85f * a));
            }

            //悬停柔光底纹，基于扇区径向覆盖
            if (hoverAmt > 0.01f) {
                Color hoverCol = enabled ? SHPCTheme.CyanHi : SHPCTheme.Disabled;
                DrawArc(sb, px, center, rIn + 1f, rOut - 1f, aStart, aEnd,
                    hoverCol * (hoverAmt * 0.18f * a));
            }

            //选中暖色高光带
            if (selectAmt > 0.01f) {
                DrawArc(sb, px, center, rOut - 5f, rOut - 1f, aStart, aEnd,
                    SHPCTheme.Accent * (selectAmt * 0.85f * a));
            }

            //外缘描边，悬停或选中时更亮
            float borderHi = MathF.Max(hoverAmt, selectAmt);
            Color borderCol = enabled
                ? Color.Lerp(SHPCTheme.Border, SHPCTheme.BorderHi, borderHi)
                : SHPCTheme.Disabled;
            DrawArcStroke(sb, px, center, rOut - 0.5f, aStart, aEnd, 1.4f, borderCol * a);
            DrawArcStroke(sb, px, center, rIn + 0.5f, aStart, aEnd, 1.1f,
                borderCol * (0.65f * a));

            //径向封口
            float capThick = 1.4f;
            Vector2 dirS = AngleDir(aStart);
            Vector2 dirE = AngleDir(aEnd);
            DrawLine(sb, px, center + dirS * (rIn + 1f), center + dirS * (rOut - 1f),
                capThick, borderCol * (0.7f * a));
            DrawLine(sb, px, center + dirE * (rIn + 1f), center + dirE * (rOut - 1f),
                capThick, borderCol * (0.7f * a));

            //图标，绘制于按钮中心半径
            if (!string.IsNullOrEmpty(glyph)) {
                float midA = (aStart + aEnd) * 0.5f;
                Vector2 iconPos = center + AngleDir(midA) * SHPCTheme.ButtonMidR;
                DynamicSpriteFont font = FontAssets.MouseText.Value;
                Vector2 size = font.MeasureString(glyph) * 0.85f;
                Color iconCol = enabled
                    ? Color.Lerp(SHPCTheme.Text, SHPCTheme.CyanHi, borderHi)
                    : SHPCTheme.Disabled;
                Utils.DrawBorderString(sb, glyph, iconPos - size * 0.5f, iconCol * a, 0.85f);
            }

            //扫光带
            if (enabled && (hoverAmt > 0.01f || selectAmt > 0.01f)) {
                float scanT = (time * 0.6f) % 1f;
                float scanA = MathHelper.Lerp(aStart, aEnd, scanT);
                float scanWidth = (aEnd - aStart) * 0.18f;
                float sa0 = MathF.Max(aStart, scanA - scanWidth * 0.5f);
                float sa1 = MathF.Min(aEnd, scanA + scanWidth * 0.5f);
                DrawArc(sb, px, center, rIn + 2f, rOut - 2f, sa0, sa1,
                    SHPCTheme.CyanHi * (0.18f * MathF.Max(hoverAmt, selectAmt) * a));
            }
        }

        /// <summary>核心到按钮连接线，展开时按扇区中线</summary>
        public static void DrawConnector(SpriteBatch sb, Texture2D px, Vector2 center,
            float midAngle, float expandProgress, float hoverAmt, float globalAlpha) {
            if (expandProgress < 0.05f) {
                return;
            }
            float ease = EaseOutCubic(MathHelper.Clamp(expandProgress, 0f, 1f));
            Vector2 dir = AngleDir(midAngle);
            Vector2 p0 = center + dir * (SHPCTheme.CoreRingR + 4f);
            Vector2 p1 = center + dir * MathHelper.Lerp(SHPCTheme.CoreRingR + 8f,
                SHPCTheme.ButtonInnerR - 1f, ease);
            Color col = Color.Lerp(SHPCTheme.Border, SHPCTheme.BorderHi, hoverAmt) *
                (ease * globalAlpha);
            DrawLine(sb, px, p0, p1, 1.2f, col);
            //端点小圆点
            DrawDisc(sb, px, p1, 1.6f, 1.4f, col * 1.2f);
        }

        #endregion

        #region 二级信息面板

        /// <summary>二级信息面板，tooltip跟光标，贴边翻转，描述换行</summary>
        public static void DrawInfoPanel(SpriteBatch sb, Texture2D px,
            Vector2 cursor, float panelAlpha, float globalAlpha,
            string title, string subtitle, string description, string statusText) {
            if (panelAlpha < 0.02f) {
                return;
            }
            float a = panelAlpha * globalAlpha;
            float textSize = 1.25f;
            float pad = 6f;
            float descScale = 0.52f * textSize;
            float minPanelW = 168f * textSize;
            float minPanelH = 60f * textSize;
            float descStartY = 40f;
            float bottomPad = 8f;

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float panelW = minPanelW;
            int maxTextW = (int)(panelW - pad * 2f);

            //提前换行以确定实际行数，驱动面板高度
            string[] descLines = string.IsNullOrEmpty(description)
                ? []
                : VaultUtils.WrapTextArray(description, font, maxTextW + 112, 99, out _);
            float lineH = font.MeasureString("A").Y * descScale;
            int validLineCount = 0;
            foreach (string l in descLines) {
                if (!string.IsNullOrEmpty(l)) validLineCount++;
            }
            float panelH = validLineCount > 0
                ? Math.Max(minPanelH, descStartY + validLineCount * lineH + bottomPad)
                : minPanelH;

            //入场偏移，沿光标右下方向滑入
            float slide = (1f - panelAlpha) * 8f;
            //默认光标右下
            Vector2 panelPos = cursor + new Vector2(18f + slide, 14f);
            //贴边翻转
            if (panelPos.X + panelW > Main.screenWidth - 8f) {
                panelPos.X = cursor.X - panelW - 18f - slide;
            }
            if (panelPos.Y + panelH > Main.screenHeight - 8f) {
                panelPos.Y = cursor.Y - panelH - 14f;
            }
            //硬限位
            panelPos.X = MathHelper.Clamp(panelPos.X, 4f, Main.screenWidth - panelW - 4f);
            panelPos.Y = MathHelper.Clamp(panelPos.Y, 4f, Main.screenHeight - panelH - 4f);
            Rectangle rect = new((int)panelPos.X, (int)panelPos.Y, (int)panelW, (int)panelH);

            //投影
            DrawFilledRect(sb, px, new Rectangle(rect.X + 2, rect.Y + 3, rect.Width, rect.Height),
                SHPCTheme.ShadowDark * (0.55f * a));

            //背景
            DrawFilledRect(sb, px, rect, SHPCTheme.SlotBg * (0.92f * a));

            //顶部色带
            DrawFilledRect(sb, px,
                new Rectangle(rect.X, rect.Y, rect.Width, 3), SHPCTheme.Cyan * (0.85f * a));

            //描边
            DrawRectStroke(sb, px, rect, 1.2f, SHPCTheme.Border * (0.85f * a));

            //左侧竖线装饰
            DrawLine(sb, px,
                new Vector2(rect.X, rect.Y + 6), new Vector2(rect.X, rect.Y + rect.Height - 6),
                1.6f, SHPCTheme.Cyan * (0.55f * a));
            //四角L形装饰
            DrawCornerBrackets(sb, px, rect, 6f, 1.4f, SHPCTheme.BorderHi * (0.85f * a));

            //标题
            if (!string.IsNullOrEmpty(title)) {
                Utils.DrawBorderString(sb, title,
                    new Vector2(rect.X + pad, rect.Y + 6f), SHPCTheme.Text * a, 0.6f * textSize);
            }
            //副标题
            if (!string.IsNullOrEmpty(subtitle)) {
                Utils.DrawBorderString(sb, subtitle,
                    new Vector2(rect.X + pad, rect.Y + 22f), SHPCTheme.TextDim * a, 0.5f * textSize);
            }
            //状态值，绘制于右上角
            if (!string.IsNullOrEmpty(statusText)) {
                Vector2 size = font.MeasureString(statusText) * 0.5f * textSize;
                Utils.DrawBorderString(sb, statusText,
                    new Vector2(rect.Right - pad - size.X, rect.Y + 6f),
                    SHPCTheme.CyanHi * a, 0.5f * textSize);
            }
            //说明，逐行绘制
            float descY = rect.Y + descStartY;
            foreach (string line in descLines) {
                if (string.IsNullOrEmpty(line)) continue;
                Utils.DrawBorderString(sb, line.TrimEnd('-', ' '),
                    new Vector2(rect.X + pad, descY),
                    SHPCTheme.TextDim * a, descScale);
                descY += lineH;
            }
        }

        #endregion

        #region 矩形辅助

        public static void DrawFilledRect(SpriteBatch sb, Texture2D px, Rectangle rect, Color color) {
            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1), color);
        }

        public static void DrawRectStroke(SpriteBatch sb, Texture2D px, Rectangle rect, float thickness, Color color) {
            //四边线段，防叠色
            Vector2 tl = new(rect.X, rect.Y);
            Vector2 tr = new(rect.Right, rect.Y);
            Vector2 bl = new(rect.X, rect.Bottom);
            Vector2 br = new(rect.Right, rect.Bottom);
            DrawLine(sb, px, tl, tr, thickness, color);
            DrawLine(sb, px, bl, br, thickness, color);
            DrawLine(sb, px, tl, bl, thickness, color);
            DrawLine(sb, px, tr, br, thickness, color);
        }

        public static void DrawCornerBrackets(SpriteBatch sb, Texture2D px, Rectangle rect, float size, float thickness, Color color) {
            //左上
            DrawLine(sb, px, new Vector2(rect.X, rect.Y), new Vector2(rect.X + size, rect.Y), thickness, color);
            DrawLine(sb, px, new Vector2(rect.X, rect.Y), new Vector2(rect.X, rect.Y + size), thickness, color);
            //右上
            DrawLine(sb, px, new Vector2(rect.Right - size, rect.Y), new Vector2(rect.Right, rect.Y), thickness, color);
            DrawLine(sb, px, new Vector2(rect.Right, rect.Y), new Vector2(rect.Right, rect.Y + size), thickness, color);
            //左下
            DrawLine(sb, px, new Vector2(rect.X, rect.Bottom - size), new Vector2(rect.X, rect.Bottom), thickness, color);
            DrawLine(sb, px, new Vector2(rect.X, rect.Bottom), new Vector2(rect.X + size, rect.Bottom), thickness, color);
            //右下
            DrawLine(sb, px, new Vector2(rect.Right - size, rect.Bottom), new Vector2(rect.Right, rect.Bottom), thickness, color);
            DrawLine(sb, px, new Vector2(rect.Right, rect.Bottom - size), new Vector2(rect.Right, rect.Bottom), thickness, color);
        }

        #endregion

        #region RAM弧形条

        //弧形几何常量，与SHPC核心环尺寸匹配
        //中线=旧Ram弧中心角
        private const float RamMidAngle = 3.575f;
        private const float RamInnerR = 28f;
        private const float RamOuterR = 50f;
        private const float RamCellGap = 0.04f;
        //装饰环稍微超出弧带边缘
        private const float RamDecoOuterR = RamOuterR + 5f;
        private const float RamDecoInnerR = RamInnerR - 4f;
        //单格角，8格+7隙=旧2.15rad
        //BaseCellAngle = (2.15 - 7*0.04) / 8 ≈ 0.234 rad
        private const float RamBaseCellAngle = 0.234f;
        //格子模式最大跨度，再上切百分比
        private const float RamGridModeMaxSweep = 0.85f * MathHelper.TwoPi;
        //跨度软上限满圆，超永久上限保持满圆
        private const float RamMaxTotalSweep = MathHelper.TwoPi;

        /// <summary>由MaxRam算单格角与弧起止，高容量切百分比并渐闭合</summary>
        private static void ComputeRamArcParams(int maxRam,
            out float aStart, out float aEnd, out float cellAngle, out float totalSweep, out bool percentageMode) {
            float targetSweep = RamBaseCellAngle * maxRam + (maxRam - 1) * RamCellGap;
            if (targetSweep <= RamGridModeMaxSweep) {
                cellAngle = RamBaseCellAngle;
                totalSweep = targetSweep;
                percentageMode = false;
            }
            else {
                //格子过密后改用连续弧；弧长表达容量成长，填充表达当前 RAM 比例
                int gridModeMaxRam = Math.Max(1, (int)MathF.Floor((RamGridModeMaxSweep + RamCellGap) / (RamBaseCellAngle + RamCellGap)));
                float gridModeMaxSweep = RamBaseCellAngle * gridModeMaxRam + (gridModeMaxRam - 1) * RamCellGap;
                float maxUpgradeableRam = RamSystem.DefaultBaseMaxRam
                    + RamSystem.MaxCapacityUpgradeChips * RamSystem.CapacityUpgradeChipBonus;
                float grow = MathHelper.Clamp((maxRam - gridModeMaxRam) / MathF.Max(1f, maxUpgradeableRam - gridModeMaxRam), 0f, 1f);
                totalSweep = MathHelper.Lerp(gridModeMaxSweep, RamMaxTotalSweep, grow);
                cellAngle = RamBaseCellAngle;
                percentageMode = true;
            }
            //围绕固定中线对称展开，让弧条像"扇子"一样左右拉伸
            aStart = percentageMode
                ? MathHelper.PiOver2
                : RamMidAngle - totalSweep * 0.5f;
            aEnd = aStart + totalSweep;
        }

        /// <summary>左侧RAM弧，优先HackRamArc.fx，绕 <see cref="RamMidAngle"/> 对称</summary>
        public static void DrawRAMBar(SpriteBatch sb, Texture2D px, Vector2 center,
            float currentRam, int maxRam, float time, float globalAlpha) {
            if (maxRam <= 0 || globalAlpha < 0.01f) {
                return;
            }
            ComputeRamArcParams(maxRam, out float aStart, out float aEnd, out float cellAngle, out float totalSweep, out bool percentageMode);
            if (cellAngle <= 0.01f) {
                return;
            }
            float lowRam = 0f;
            if (!HackTime.InfiniteHack) {
                if (currentRam < 0.5f) {
                    lowRam = 1f;
                }
                else if (currentRam <= 2f) {
                    lowRam = MathHelper.Clamp(1f - (currentRam - 0.5f) / 1.5f, 0f, 1f);
                }
            }
            //系统锁定/RAM 不足故障闪烁直接拉到上限
            lowRam = MathF.Max(lowRam, RamSystem.GetWarningPulse());
            float lockFill = RamSystem.LockRemainRatio;
            float recoveryFill = RamSystem.RecoveryRateRatio;

            Effect effect = EffectLoader.HackRamArc?.Value;
            if (effect != null) {
                DrawRAMBar_Shader(sb, px, center, currentRam, maxRam,
                    aStart, cellAngle, totalSweep, lowRam, lockFill, recoveryFill, time, globalAlpha, percentageMode, effect);
            }
            else {
                DrawRAMBar_CPU(sb, px, center, currentRam, maxRam,
                    aStart, aEnd, cellAngle, time, globalAlpha, percentageMode, lockFill, recoveryFill);
            }
            DrawRecoveryInnerFill(sb, px, center, aStart, aEnd, recoveryFill, globalAlpha);

            //数值标签，始终CPU绘制；锚定在弧条中线外缘
            Vector2 labelDir = AngleDir(RamMidAngle);
            Vector2 labelPos = center + labelDir * (RamOuterR + 14f);
            string ramStr = $"{(int)currentRam}/{maxRam}";
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 sz = font.MeasureString(ramStr) * 0.38f;
            Utils.DrawBorderString(sb, ramStr, labelPos - sz * 0.5f, SHPCTheme.TextDim * globalAlpha, 0.58f);
        }

        private static void DrawRAMBar_Shader(SpriteBatch sb, Texture2D px, Vector2 center,
            float currentRam, int maxRam, float aStart, float cellAngle, float totalSweep,
            float lowRam, float lockFill, float recoveryFill, float time, float globalAlpha, bool percentageMode, Effect effect) {
            //包围盒盖满弧圈
            const float pad = 14f;
            float boxR = RamDecoOuterR + pad;
            float qLeft = MathF.Max(0f, center.X - boxR);
            float qTop = MathF.Max(0f, center.Y - boxR);
            float qRight = MathF.Min(Main.screenWidth, center.X + boxR);
            float qBottom = MathF.Min(Main.screenHeight, center.Y + boxR);
            int qW = (int)MathF.Ceiling(qRight - qLeft);
            int qH = (int)MathF.Ceiling(qBottom - qTop);
            if (qW <= 0 || qH <= 0) {
                return;
            }
            Rectangle dest = new((int)qLeft, (int)qTop, qW, qH);
            Vector2 relCenter = new(center.X - qLeft, center.Y - qTop);

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(globalAlpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(qW, qH));
            effect.Parameters["uArcCenter"]?.SetValue(relCenter);
            effect.Parameters["uInnerR"]?.SetValue(RamInnerR);
            effect.Parameters["uOuterR"]?.SetValue(RamOuterR);
            effect.Parameters["uAStart"]?.SetValue(aStart);
            if (percentageMode) {
                //百分比模式整段填充
                float ratio = maxRam > 0 ? MathHelper.Clamp(currentRam / maxRam, 0f, 1f) : 0f;
                effect.Parameters["uCellAngle"]?.SetValue(totalSweep);
                effect.Parameters["uCellGap"]?.SetValue(0f);
                effect.Parameters["uCellCount"]?.SetValue(1f);
                effect.Parameters["uFillValue"]?.SetValue(ratio);
            }
            else {
                effect.Parameters["uCellAngle"]?.SetValue(cellAngle);
                effect.Parameters["uCellGap"]?.SetValue(RamCellGap);
                effect.Parameters["uCellCount"]?.SetValue((float)maxRam);
                effect.Parameters["uFillValue"]?.SetValue(currentRam);
            }
            effect.Parameters["uLowRam"]?.SetValue(lowRam);
            effect.Parameters["uLockFill"]?.SetValue(lockFill);
            effect.Parameters["uRecoveryFill"]?.SetValue(recoveryFill);
            effect.Parameters["uInfinite"]?.SetValue(HackTime.InfiniteHack ? 1f : 0f);
            effect.Parameters["uDecoOuterR"]?.SetValue(RamDecoOuterR);
            effect.Parameters["uDecoInnerR"]?.SetValue(RamDecoInnerR);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            sb.Draw(px, dest, Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        private static void DrawRAMBar_CPU(SpriteBatch sb, Texture2D px, Vector2 center,
            float currentRam, int maxRam, float aStart, float aEnd, float cellAngle,
            float time, float globalAlpha, bool percentageMode, float lockFill, float recoveryFill) {
            float a = globalAlpha;
            DrawArc(sb, px, center + new Vector2(1.5f, 2f),
                RamInnerR, RamOuterR, aStart, aEnd,
                SHPCTheme.ShadowDark * (0.5f * a));
            float lockPulse = MathF.Sin(time * 8f) * 0.5f + 0.5f;
            Color lockCol = Color.Lerp(new Color(220, 45, 45), new Color(255, 95, 35), lockPulse * 0.35f);
            if (percentageMode) {
                //百分比模式无分隔线
                float ratio = maxRam > 0 ? MathHelper.Clamp(currentRam / maxRam, 0f, 1f) : 0f;
                DrawArc(sb, px, center, RamInnerR + 1f, RamOuterR - 1f, aStart, aEnd, SHPCTheme.SlotBg * (0.92f * a));
                DrawArc(sb, px, center, RamInnerR + 1f, RamInnerR + 3f, aStart, aEnd, SHPCTheme.ShadowDark * (0.5f * a));
                if (ratio > 0.01f) {
                    float fillEnd = MathHelper.Lerp(aStart, aEnd, ratio);
                    Color fillCol = ratio >= 0.98f ? SHPCTheme.Cyan : Color.Lerp(SHPCTheme.Border, SHPCTheme.Cyan, ratio);
                    DrawArc(sb, px, center, RamInnerR + 1f, RamOuterR - 2f, aStart, fillEnd, fillCol * (0.6f * a));
                    DrawArc(sb, px, center, RamOuterR - 4f, RamOuterR - 1f, aStart, fillEnd, SHPCTheme.CyanHi * (ratio * 0.85f * a));
                }
                if (lockFill > 0.001f) {
                    float lockEnd = MathHelper.Lerp(aStart, aEnd, MathHelper.Clamp(lockFill, 0f, 1f));
                    DrawArc(sb, px, center, RamInnerR + 2f, RamOuterR - 2f, aStart, lockEnd, lockCol * (0.55f * a));
                    DrawArc(sb, px, center, RamInnerR + 2f, RamInnerR + 4f, aStart, lockEnd, new Color(255, 60, 45) * (0.28f * a));
                    DrawLine(sb, px, center + AngleDir(lockEnd) * (RamInnerR + 1f), center + AngleDir(lockEnd) * (RamOuterR - 1f),
                        1.7f, Color.Lerp(SHPCTheme.Text, lockCol, 0.55f) * (0.7f * a));
                }
                DrawArcStroke(sb, px, center, RamOuterR - 0.5f, aStart, aEnd, 1.2f, SHPCTheme.BorderHi * (0.75f * a));
                DrawArcStroke(sb, px, center, RamInnerR + 0.5f, aStart, aEnd, 1.0f, SHPCTheme.Border * (0.55f * a));
                float scanT2 = (time * 0.35f) % 1f;
                float scanA2 = MathHelper.Lerp(aStart, aEnd, scanT2);
                float scanW2 = (aEnd - aStart) * 0.07f;
                DrawArc(sb, px, center, RamInnerR + 2f, RamOuterR - 2f,
                    MathF.Max(aStart, scanA2 - scanW2 * 0.5f),
                    MathF.Min(aEnd, scanA2 + scanW2 * 0.5f),
                    SHPCTheme.CyanHi * (0.1f * a));
                return;
            }
            for (int i = 0; i < maxRam; i++) {
                float cStart = aStart + i * (cellAngle + RamCellGap);
                float cEnd = cStart + cellAngle;
                float fill = MathHelper.Clamp(currentRam - i, 0f, 1f);
                DrawArc(sb, px, center, RamInnerR + 1f, RamOuterR - 1f, cStart, cEnd,
                    SHPCTheme.SlotBg * (0.92f * a));
                DrawArc(sb, px, center, RamInnerR + 1f, RamInnerR + 3f, cStart, cEnd,
                    SHPCTheme.ShadowDark * (0.5f * a));
                if (fill > 0.01f) {
                    Color fillCol = fill >= 0.98f ? SHPCTheme.Cyan : Color.Lerp(SHPCTheme.Border, SHPCTheme.Cyan, fill);
                    float fillEnd = MathHelper.Lerp(cStart, cEnd, fill);
                    DrawArc(sb, px, center, RamInnerR + 1f, RamOuterR - 2f, cStart, fillEnd, fillCol * (0.6f * a));
                    DrawArc(sb, px, center, RamOuterR - 4f, RamOuterR - 1f, cStart, fillEnd, SHPCTheme.CyanHi * (fill * 0.85f * a));
                }
                float lockCellFill = MathHelper.Clamp(lockFill * maxRam - i, 0f, 1f);
                if (lockCellFill > 0.001f) {
                    float lockEnd = MathHelper.Lerp(cStart, cEnd, lockCellFill);
                    DrawArc(sb, px, center, RamInnerR + 2f, RamOuterR - 2f, cStart, lockEnd, lockCol * (0.55f * a));
                    DrawArc(sb, px, center, RamInnerR + 2f, RamInnerR + 4f, cStart, lockEnd, new Color(255, 60, 45) * (0.28f * a));
                    if (lockCellFill < 0.999f) {
                        DrawLine(sb, px, center + AngleDir(lockEnd) * (RamInnerR + 1f), center + AngleDir(lockEnd) * (RamOuterR - 1f),
                            1.7f, Color.Lerp(SHPCTheme.Text, lockCol, 0.55f) * (0.7f * a));
                    }
                }
                Color borderCol = fill >= 0.98f ? SHPCTheme.BorderHi : SHPCTheme.Border;
                DrawArcStroke(sb, px, center, RamOuterR - 0.5f, cStart, cEnd, 1.2f, borderCol * (0.75f * a));
                DrawArcStroke(sb, px, center, RamInnerR + 0.5f, cStart, cEnd, 1.0f, borderCol * (0.55f * a));
                DrawLine(sb, px, center + AngleDir(cStart) * (RamInnerR + 1f), center + AngleDir(cStart) * (RamOuterR - 1f), 1.2f, borderCol * (0.55f * a));
                DrawLine(sb, px, center + AngleDir(cEnd) * (RamInnerR + 1f), center + AngleDir(cEnd) * (RamOuterR - 1f), 1.2f, borderCol * (0.55f * a));
            }
            float scanT = (time * 0.35f) % 1f;
            float scanA = MathHelper.Lerp(aStart, aEnd, scanT);
            float scanW = (aEnd - aStart) * 0.07f;
            DrawArc(sb, px, center, RamInnerR + 2f, RamOuterR - 2f,
                MathF.Max(aStart, scanA - scanW * 0.5f),
                MathF.Min(aEnd, scanA + scanW * 0.5f),
                SHPCTheme.CyanHi * (0.1f * a));
        }

        private static void DrawRecoveryInnerFill(SpriteBatch sb, Texture2D px, Vector2 center,
            float aStart, float aEnd, float recoveryFill, float alpha) {
            if (alpha < 0.01f) {
                return;
            }

            float recovery = MathHelper.Clamp(recoveryFill, 0f, 1f);
            DrawArc(sb, px, center, RamInnerR - 7f, RamInnerR - 2f, aStart, aEnd,
                SHPCTheme.ShadowDark * (0.55f * alpha));
            DrawArc(sb, px, center, RamInnerR - 6f, RamInnerR - 3f, aStart, aEnd,
                SHPCTheme.Border * (0.42f * alpha));
            if (recovery <= 0.001f) {
                return;
            }

            float fillEnd = MathHelper.Lerp(aStart, aEnd, recovery);
            Color recoveryCol = Color.Lerp(SHPCTheme.Cyan, SHPCTheme.CyanHi, recovery * 0.65f);
            DrawArc(sb, px, center, RamInnerR - 7f, RamInnerR - 2f, aStart, fillEnd,
                recoveryCol * (0.26f * alpha));
            DrawArc(sb, px, center, RamInnerR - 6f, RamInnerR - 3f, aStart, fillEnd,
                recoveryCol * (0.88f * alpha));
            if (recovery < 0.999f) {
                DrawLine(sb, px,
                    center + AngleDir(fillEnd) * (RamInnerR - 8f),
                    center + AngleDir(fillEnd) * (RamInnerR - 1f),
                    1.9f, Color.Lerp(SHPCTheme.Text, recoveryCol, 0.55f) * (0.95f * alpha));
            }
        }

        #endregion
    }
}
