using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Onikiri
{
    /// <summary>笔触见 <see cref="OniBrush"/>,此处薄委托</summary>
    internal static class OnikiriPanelDraw
    {
        /// <summary>阴影 + OniNarrativePanel,无 shader 走 CPU</summary>
        public static void DrawShaderBackground(SpriteBatch spriteBatch, Rectangle rect, float alpha, OnikiriPanelState state) {
            //阴影按 alpha²,揭示期不抢戏
            SkinDrawUtil.DrawPanelShadow(spriteBatch, rect, new Color(8, 2, 5) * (alpha * alpha * 0.62f), 6, 8);

            if (!OniShaderPanel.Available) {
                DrawFallbackPanel(spriteBatch, rect, alpha);
                return;
            }

            //reveal 跟开合,体不透明度快上斜
            float body = Math.Min(1f, alpha * 1.6f);
            OniShaderPanel.Draw(spriteBatch, rect, body, alpha, state.ShaderTime, OnikiriPanelState.ShaderEdgePad, Color.White);
        }

        /// <summary>CPU 降级,墨底双描边</summary>
        public static void DrawFallbackPanel(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            spriteBatch.Draw(pixel, rect, src, OnikiriPanelState.Ink * (alpha * 0.96f));
            SkinDrawUtil.DrawRectBorder(spriteBatch, rect, OnikiriPanelState.Deep * (alpha * 0.58f), 2);
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            SkinDrawUtil.DrawRectBorder(spriteBatch, inner, OnikiriPanelState.Dark * (alpha * 0.85f), 1);
            spriteBatch.Draw(pixel, new Rectangle(rect.X + 8, rect.Y - 4, rect.Width - 16, 3), src, OnikiriPanelState.Deep * (alpha * 0.5f));
        }

        /// <summary>纸垂落点同 shader 绸带下垂公式,不进正文</summary>
        public static void DrawShide(SpriteBatch spriteBatch, Rectangle rect, float alpha, float swayTimer)
            => OniBrush.DrawShide(spriteBatch, rect, alpha, swayTimer);

        /// <summary>朱印方章,rotation 盖章用</summary>
        public static void DrawSealGlyph(SpriteBatch spriteBatch, Vector2 center, float size, float alpha, float rotation = 0f)
            => OniBrush.DrawSealGlyph(spriteBatch, center, size, alpha, rotation);

        /// <summary>刀痕笔触,sweep 0~1 截断长度</summary>
        public static void DrawTaperedSlash(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float maxThick, float bow, float alpha, float sweep = 1f)
            => OniBrush.DrawTaperedSlash(spriteBatch, start, end, maxThick, bow, alpha, sweep);

        /// <summary>四角朱笔角签(弹窗)</summary>
        public static void DrawCornerTicks(SpriteBatch spriteBatch, Rectangle rect, float alpha, float pulse) {
            float a = alpha * (0.55f + pulse * 0.2f);
            const float len = 12f;
            const int inset = 4;
            DrawTaperedSlash(spriteBatch, new Vector2(rect.X + inset, rect.Y + inset + 1), new Vector2(rect.X + inset + len, rect.Y + inset + 1), 1.7f, 0.6f, a);
            DrawTaperedSlash(spriteBatch, new Vector2(rect.X + inset + 1, rect.Y + inset), new Vector2(rect.X + inset + 1, rect.Y + inset + len), 1.7f, 0.6f, a);
            DrawTaperedSlash(spriteBatch, new Vector2(rect.Right - inset - len, rect.Bottom - inset - 1), new Vector2(rect.Right - inset, rect.Bottom - inset - 1), 1.7f, 0.6f, a * 0.85f);
            DrawTaperedSlash(spriteBatch, new Vector2(rect.Right - inset - 1, rect.Bottom - inset - len), new Vector2(rect.Right - inset - 1, rect.Bottom - inset), 1.7f, 0.6f, a * 0.85f);
        }

        /// <summary>绘马挂绳(弹窗)</summary>
        public static void DrawHangingKnot(SpriteBatch spriteBatch, Rectangle rect, float alpha, float swayTimer)
            => OniBrush.DrawHangingKnot(spriteBatch, rect, alpha, swayTimer);
    }
}
