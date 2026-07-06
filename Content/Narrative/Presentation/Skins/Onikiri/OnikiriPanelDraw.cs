using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Onikiri
{
    /// <summary>
    /// 鬼切叙事皮肤的静态绘制件:面板背景(shader/CPU 降级)与皮肤专属构图。<br/>
    /// 笔触原语(刀痕/朱印/纸垂/挂绳)已提炼至 <see cref="OniBrush"/> 与点鬼簿三屏共用,此处保留薄委托
    /// </summary>
    internal static class OnikiriPanelDraw
    {
        /// <summary>面板背景:阴影 + OniNarrativePanel.fx;shader 缺失时走 CPU 降级</summary>
        public static void DrawShaderBackground(SpriteBatch spriteBatch, Rectangle rect, float alpha, OnikiriPanelState state) {
            //阴影按 alpha 平方衰减:拔刀揭示还只是一条线时不能先出现整块暗影
            SkinDrawUtil.DrawPanelShadow(spriteBatch, rect, new Color(8, 2, 5) * (alpha * alpha * 0.62f), 6, 8);

            if (!OniShaderPanel.Available) {
                DrawFallbackPanel(spriteBatch, rect, alpha);
                return;
            }

            //reveal 直接吃面板开合进度;面板体不透明度快速上斜,避免"半透明面板"长时间存在
            float body = Math.Min(1f, alpha * 1.6f);
            OniShaderPanel.Draw(spriteBatch, rect, body, alpha, state.ShaderTime, OnikiriPanelState.ShaderEdgePad, Color.White);
        }

        /// <summary>CPU 降级面板:墨黑底 + 深红双描边 + 顶沿绸线残影,保证无 shader 时依然成立</summary>
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

        /// <summary>
        /// 纸垂:两条白纸之字形垂片挂在顶沿注连墨绸上。
        /// 落点与 shader 绸带的中央下垂公式同源(sin(πu)*3.4),纸垂长度只吃边沿带,不进正文区
        /// </summary>
        public static void DrawShide(SpriteBatch spriteBatch, Rectangle rect, float alpha, float swayTimer)
            => OniBrush.DrawShide(spriteBatch, rect, alpha, swayTimer);

        /// <summary>朱印方章:阴影/深红衬底/朱红章体/纸白刻痕(简化印文)。rotation 供盖章动画用</summary>
        public static void DrawSealGlyph(SpriteBatch spriteBatch, Vector2 center, float size, float alpha, float rotation = 0f)
            => OniBrush.DrawSealGlyph(spriteBatch, center, size, alpha, rotation);

        /// <summary>
        /// 刀痕笔触:两端收尖、中段最宽、带轻微上弓的渐变笔画,底色深红、前段叠白热芯。
        /// 分隔线与选项扫线共用;sweep 取 0~1 截断绘制长度(hover 扫入动画)
        /// </summary>
        public static void DrawTaperedSlash(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float maxThick, float bow, float alpha, float sweep = 1f)
            => OniBrush.DrawTaperedSlash(spriteBatch, start, end, maxThick, bow, alpha, sweep);

        /// <summary>四角朱笔角签:短促的收笔笔触压住面板四角(弹窗用)</summary>
        public static void DrawCornerTicks(SpriteBatch spriteBatch, Rectangle rect, float alpha, float pulse) {
            float a = alpha * (0.55f + pulse * 0.2f);
            const float len = 12f;
            const int inset = 4;
            DrawTaperedSlash(spriteBatch, new Vector2(rect.X + inset, rect.Y + inset + 1), new Vector2(rect.X + inset + len, rect.Y + inset + 1), 1.7f, 0.6f, a);
            DrawTaperedSlash(spriteBatch, new Vector2(rect.X + inset + 1, rect.Y + inset), new Vector2(rect.X + inset + 1, rect.Y + inset + len), 1.7f, 0.6f, a);
            DrawTaperedSlash(spriteBatch, new Vector2(rect.Right - inset - len, rect.Bottom - inset - 1), new Vector2(rect.Right - inset, rect.Bottom - inset - 1), 1.7f, 0.6f, a * 0.85f);
            DrawTaperedSlash(spriteBatch, new Vector2(rect.Right - inset - 1, rect.Bottom - inset - len), new Vector2(rect.Right - inset - 1, rect.Bottom - inset), 1.7f, 0.6f, a * 0.85f);
        }

        /// <summary>绘马挂绳:两根斜绳收到顶结,结下垂一缕随摆的流苏(弹窗用)</summary>
        public static void DrawHangingKnot(SpriteBatch spriteBatch, Rectangle rect, float alpha, float swayTimer)
            => OniBrush.DrawHangingKnot(spriteBatch, rect, alpha, swayTimer);
    }
}
