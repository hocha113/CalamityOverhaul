using CalamityOverhaul.Common;
using CalamityOverhaul.Content.EntrustManager;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    /// <summary>
    /// 超梦教程卡片/面板的共享绘制样式。
    /// 三个教程 Lead 与两块面板此前各自复制了整套 DrawCardBg/DrawNextButton/DrawLBrackets，
    /// 收进此处统一维护；视觉骨架不变（EntrustGuideCard 背景 + 1px 描边 + L 角标）
    /// </summary>
    internal static class CybCourseCardStyle
    {
        //教程卡片几何
        public const int CardW = 310;
        public const int CardH = 118;
        public const int EdgePad = 8;

        //字号
        public const float TitleScale = 0.84f;
        public const float BodyScale = 0.70f;
        public const float SubScale = 0.58f;

        //青系色板
        public static readonly Color TitleColor = new(80, 220, 245);
        public static readonly Color CounterColor = new(70, 155, 175);
        public static readonly Color DividerColor = new(45, 130, 155);
        public static readonly Color BodyColor = new(175, 215, 225);
        public static readonly Color StuckHintColor = new(255, 110, 90);
        public static readonly Color KeyHintColor = new(255, 195, 90);
        public static readonly Color StatusColor = new(60, 190, 200);
        public static readonly Color BracketColor = new(80, 220, 245);

        //==================== 背景 ====================

        /// <summary>青系卡片背景（EntrustGuideCard uVariant=1；缺 shader 走描边矩形）</summary>
        public static void DrawCardBg(SpriteBatch sb, Rectangle card, float alpha, float shaderTimer)
            => DrawShaderBg(sb, card, alpha, shaderTimer, EdgePad, 0.96f, 1f,
                new Color(0, 8, 18, (int)(200 * alpha)),
                new Color(50, 160, 200, (int)(120 * alpha)));

        /// <summary>面板背景；amber=true 走暖琥珀变体（uVariant=0）</summary>
        public static void DrawPanelBg(SpriteBatch sb, Rectangle panel, float alpha, float shaderTimer, bool amber) {
            Color fallbackBg = amber
                ? new Color(20, 14, 4, (int)(220 * alpha))
                : new Color(0, 8, 18, (int)(220 * alpha));
            Color fallbackBorder = amber
                ? new Color(220, 170, 70, (int)(170 * alpha))
                : new Color(50, 160, 200, (int)(160 * alpha));
            DrawShaderBg(sb, panel, alpha, shaderTimer, 10, 0.97f, amber ? 0f : 1f, fallbackBg, fallbackBorder);
        }

        private static void DrawShaderBg(SpriteBatch sb, Rectangle rect, float alpha, float shaderTimer,
            int edgePad, float alphaMul, float variant, Color fallbackBg, Color fallbackBorder) {
            Effect effect = EffectLoader.EntrustGuideCard?.Value;
            if (effect != null) {
                Rectangle ext = rect;
                ext.Inflate(edgePad, edgePad);
                effect.Parameters["uTime"]?.SetValue(shaderTimer);
                effect.Parameters["uAlpha"]?.SetValue(alpha * alphaMul);
                effect.Parameters["uResolution"]?.SetValue(new Vector2(ext.Width, ext.Height));
                effect.Parameters["uEdgePad"]?.SetValue((float)edgePad);
                effect.Parameters["uVariant"]?.SetValue(variant);
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, effect, Main.UIScaleMatrix);
                sb.Draw(VaultAsset.placeholder2.Value, ext, Color.White);
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, null, Main.UIScaleMatrix);
            }
            else {
                sb.Draw(VaultAsset.placeholder2.Value, rect, fallbackBg);
                BaseManagerStyle.StrokeRect(sb, rect, 1, fallbackBorder);
            }
        }

        //==================== 卡片内容件 ====================

        /// <summary>计数角标 + 标题 + 分隔线；返回正文起始 y</summary>
        public static float DrawHeader(SpriteBatch sb, Rectangle card, float alpha, string title, string counter) {
            var font = FontAssets.MouseText.Value;
            float lineT = font.MeasureString("A").Y * TitleScale + 2f;
            float px = card.X + 14f;
            float py = card.Y + 12f;

            float counterW = font.MeasureString(counter).X * SubScale;
            Utils.DrawBorderString(sb, counter,
                new Vector2(card.Right - 14f - counterW, py),
                CounterColor with { A = (byte)(150 * alpha) }, SubScale);

            Utils.DrawBorderString(sb, title, new Vector2(px, py),
                TitleColor with { A = (byte)(255 * alpha) }, TitleScale);
            py += lineT + 2f;

            BaseManagerStyle.FillRect(sb,
                new Rectangle((int)px, (int)py, CardW - 28, 1),
                DividerColor with { A = (byte)(90 * alpha) });
            return py + 6f;
        }

        /// <summary>逐行换行正文；y 随行推进</summary>
        public static void DrawBodyLines(SpriteBatch sb, Rectangle card, ref float y, float alpha, string body) {
            var font = FontAssets.MouseText.Value;
            float lineB = font.MeasureString("A").Y * BodyScale + 1f;
            float px = card.X + 14f;
            int wrapW = (int)((CardW - 28) / BodyScale);
            foreach (string line in body.Split('\n')) {
                string[] wrapped = VaultUtils.WrapTextArray(line, font, wrapW, 99, out _);
                foreach (string wl in wrapped) {
                    if (string.IsNullOrEmpty(wl)) continue;
                    Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '), new Vector2(px, y),
                        BodyColor with { A = (byte)(215 * alpha) }, BodyScale);
                    y += lineB;
                }
            }
        }

        /// <summary>琥珀脉冲提示行（未绑键等警示），y 随行推进</summary>
        public static void DrawKeyHintLines(SpriteBatch sb, Rectangle card, ref float y, float alpha,
            float shaderTimer, string text) {
            var font = FontAssets.MouseText.Value;
            float lineB = font.MeasureString("A").Y * BodyScale + 1f;
            float px = card.X + 14f;
            float pulse = 0.75f + 0.25f * MathF.Sin(shaderTimer * 10f);
            int wrapW = (int)((CardW - 28) / SubScale);
            string[] wrapped = VaultUtils.WrapTextArray(text, font, wrapW, 99, out _);
            foreach (string wl in wrapped) {
                if (string.IsNullOrEmpty(wl)) continue;
                Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '), new Vector2(px, y),
                    KeyHintColor with { A = (byte)(220 * alpha * pulse) }, SubScale);
                y += lineB - 1f;
            }
        }

        /// <summary>卡住太久的兜底提示（左下角红脉冲）</summary>
        public static void DrawStuckHint(SpriteBatch sb, Rectangle card, float alpha, float shaderTimer, string text) {
            float pulse = 0.7f + 0.3f * MathF.Sin(shaderTimer * 14f);
            Utils.DrawBorderString(sb, text,
                new Vector2(card.X + 14f, card.Bottom - 36f),
                StuckHintColor with { A = (byte)(220 * alpha * pulse) }, SubScale);
        }

        /// <summary>右下角闪烁状态角标（自动步等待中）</summary>
        public static void DrawStatusTag(SpriteBatch sb, Rectangle card, float alpha, float shaderTimer, string text) {
            var font = FontAssets.MouseText.Value;
            float blink = 0.72f + 0.28f * MathF.Sin(shaderTimer * 22f);
            float w = font.MeasureString(text).X * SubScale;
            Utils.DrawBorderString(sb, text,
                new Vector2(card.Right - 14f - w, card.Bottom - 16f),
                StatusColor with { A = (byte)(200 * alpha * blink) }, SubScale);
        }

        /// <summary>NEXT 兜底按钮；返回命中矩形供 Lead 记录</summary>
        public static Rectangle DrawNextButton(SpriteBatch sb, Rectangle card, float alpha, bool stuck,
            float shaderTimer, string label, Point mouse) {
            const int btnW = 72, btnH = 20, margin = 10;
            var btn = new Rectangle(card.Right - btnW - margin, card.Bottom - btnH - margin, btnW, btnH);

            bool hovered = btn.Contains(mouse);
            float emphasize = stuck ? 0.85f + 0.15f * MathF.Sin(shaderTimer * 14f) : 0f;
            Color bgColor = hovered
                ? new Color(40, 155, 180, (int)(210 * alpha))
                : new Color(18 + (int)(40 * emphasize), 72, 92, (int)((150 + 50 * emphasize) * alpha));
            Color borderColor = hovered
                ? new Color(100, 220, 245, (int)(200 * alpha))
                : new Color(50 + (int)(80 * emphasize), 150, 180, (int)((120 + 80 * emphasize) * alpha));
            Color textColor = hovered
                ? new Color(200, 250, 255, (int)(255 * alpha))
                : new Color(110 + (int)(80 * emphasize), 205, 225, (int)((195 + 60 * emphasize) * alpha));

            BaseManagerStyle.FillRect(sb, btn, bgColor);
            BaseManagerStyle.StrokeRect(sb, btn, 1, borderColor);
            BaseManagerStyle.DrawCenteredText(sb, label, btn.Center.ToVector2(), textColor, 0.60f);
            return btn;
        }

        /// <summary>L 形四角括标</summary>
        public static void DrawLBrackets(SpriteBatch sb, Texture2D px, Rectangle r, Color c, int len = 14) {
            const int thick = 2;
            sb.Draw(px, new Rectangle(r.Left, r.Top, len, thick), c);
            sb.Draw(px, new Rectangle(r.Left, r.Top, thick, len), c);
            sb.Draw(px, new Rectangle(r.Right - len, r.Top, len, thick), c);
            sb.Draw(px, new Rectangle(r.Right - thick, r.Top, thick, len), c);
            sb.Draw(px, new Rectangle(r.Left, r.Bottom - thick, len, thick), c);
            sb.Draw(px, new Rectangle(r.Left, r.Bottom - len, thick, len), c);
            sb.Draw(px, new Rectangle(r.Right - len, r.Bottom - thick, len, thick), c);
            sb.Draw(px, new Rectangle(r.Right - thick, r.Bottom - len, thick, len), c);
        }

        //==================== 面板件 ====================

        /// <summary>面板顶部呼吸横线</summary>
        public static void DrawBreathLine(SpriteBatch sb, Rectangle panel, float alpha, float shaderTimer, Color color) {
            float breath = 0.55f + 0.45f * MathF.Sin(shaderTimer * 4f);
            BaseManagerStyle.FillRect(sb,
                new Rectangle(panel.X + 14, panel.Y + 8, panel.Width - 28, 2),
                color with { A = (byte)(color.A * alpha * breath) });
        }

        /// <summary>居中分隔线 + 中央菱形节点</summary>
        public static void DrawDividerGem(SpriteBatch sb, Rectangle panel, int y, float alpha,
            Color lineColor, Color gemColor) {
            int divW = (int)(panel.Width * 0.55f);
            int divX = panel.Center.X - divW / 2;
            Color line = lineColor with { A = (byte)(lineColor.A * alpha) };
            BaseManagerStyle.FillRect(sb, new Rectangle(divX, y, divW / 2 - 6, 1), line);
            BaseManagerStyle.FillRect(sb, new Rectangle(divX + divW / 2 + 6, y, divW / 2 - 6, 1), line);
            BaseManagerStyle.FillRect(sb,
                new Rectangle(panel.Center.X - 3, y - 1, 6, 3),
                gemColor with { A = (byte)(gemColor.A * alpha) });
        }

        /// <summary>面板主按钮（带端帽）；amber=true 走暖琥珀色板</summary>
        public static void DrawPanelButton(SpriteBatch sb, Rectangle rect, string text, bool hot, bool amber,
            float alpha, float shaderTimer, Point mouse) {
            bool hovered = rect.Contains(mouse);
            Color baseBg, hoverBg, baseBorder, hoverBorder, baseText;
            if (amber) {
                baseBg = hot ? new Color(110, 70, 18) : new Color(72, 50, 22);
                hoverBg = hot ? new Color(200, 140, 40) : new Color(150, 110, 50);
                baseBorder = hot ? new Color(240, 190, 90) : new Color(190, 150, 80);
                hoverBorder = hot ? new Color(255, 230, 150) : new Color(240, 200, 130);
                baseText = hot ? new Color(255, 230, 180) : new Color(230, 205, 160);
            }
            else {
                baseBg = hot ? new Color(20, 90, 110) : new Color(16, 60, 78);
                hoverBg = hot ? new Color(50, 175, 200) : new Color(40, 130, 150);
                baseBorder = hot ? new Color(70, 200, 230) : new Color(60, 150, 170);
                hoverBorder = hot ? new Color(120, 240, 255) : new Color(110, 220, 240);
                baseText = hot ? new Color(170, 235, 245) : new Color(160, 215, 225);
            }
            Color hoverText = amber ? new Color(255, 250, 220) : new Color(225, 250, 255);

            float pulse = hovered ? 1f : 0.85f + 0.15f * MathF.Sin(shaderTimer * 5f);
            Color bg = (hovered ? hoverBg : baseBg) * (alpha * 0.95f * pulse);
            Color border = (hovered ? hoverBorder : baseBorder) * alpha;
            Color textCol = (hovered ? hoverText : baseText) * alpha;

            BaseManagerStyle.FillRect(sb, rect, bg);
            BaseManagerStyle.StrokeRect(sb, rect, 1, border);
            BaseManagerStyle.DrawCenteredText(sb, text, rect.Center.ToVector2(), textCol, 0.78f);

            int capH = 6;
            BaseManagerStyle.FillRect(sb,
                new Rectangle(rect.X - 2, rect.Y + rect.Height / 2 - capH, 4, capH * 2), border);
            BaseManagerStyle.FillRect(sb,
                new Rectangle(rect.Right - 2, rect.Y + rect.Height / 2 - capH, 4, capH * 2), border);
        }
    }
}
