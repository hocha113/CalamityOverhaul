using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI
{
    /// <summary>鬼伞悬浮说明行（默认字号 0.9：悬停介绍不再眯眼）</summary>
    internal readonly struct KikasaTipLine(string text, Color color, float scale = 0.9f)
    {
        public readonly string Text = text;
        public readonly Color Color = color;
        public readonly float Scale = scale;
    }

    /// <summary>
    /// 鬼伞悬浮说明的排版与四边约束（版式数学镜像 OniTooltipPanel，皮换血湖：
    /// 暗水玻璃底 + 顶缘水线 + 题下泡沫细线）。题行 1.0 与原版 tooltip 同级
    /// </summary>
    internal static class KikasaTipPanel
    {
        private const int ScreenPadding = 8;
        private const float PanelPaddingX = 12f;
        private const float MaxPanelWidth = 430f;

        private readonly struct DrawLine(string text, Color color, float scale, float gapAfter)
        {
            public readonly string Text = text;
            public readonly Color Color = color;
            public readonly float Scale = scale;
            public readonly float GapAfter = gapAfter;
        }

        public static void Draw(SpriteBatch sb, Vector2 cursor, string title, float rain,
            float alpha, params KikasaTipLine[] body) {
            if (alpha <= 0.02f || string.IsNullOrEmpty(title)) {
                return;
            }
            const float titleScale = 1.0f;

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float availablePanelWidth = Math.Max(1f, KikasaHudTheme.UIScreenW - ScreenPadding * 2f);
            float panelWidthLimit = Math.Min(MaxPanelWidth, availablePanelWidth);
            float contentWidthLimit = Math.Max(1f, panelWidthLimit - PanelPaddingX * 2f);
            float naturalWidth = font.MeasureString(title).X * titleScale;
            foreach (KikasaTipLine line in body) {
                if (!string.IsNullOrEmpty(line.Text)) {
                    naturalWidth = Math.Max(naturalWidth, font.MeasureString(line.Text).X * line.Scale);
                }
            }

            float minContentWidth = Math.Min(80f, contentWidthLimit);
            float contentWidth = MathHelper.Clamp(naturalWidth, minContentWidth, contentWidthLimit);
            float measuredTitleWidth = font.MeasureString(title).X;
            float drawTitleScale = measuredTitleWidth > 0f
                ? Math.Min(titleScale, contentWidth / measuredTitleWidth)
                : titleScale;

            List<DrawLine> drawLines = [];
            foreach (KikasaTipLine source in body) {
                if (string.IsNullOrEmpty(source.Text)) {
                    continue;
                }
                List<string> wrapped = VaultUtils.WrapText(source.Text, font, contentWidth, source.Scale);
                for (int i = 0; i < wrapped.Count; i++) {
                    string text = wrapped[i].TrimEnd();
                    if (string.IsNullOrEmpty(text)) {
                        continue;
                    }
                    float measuredWidth = font.MeasureString(text).X;
                    float drawScale = measuredWidth > 0f
                        ? Math.Min(source.Scale, contentWidth / measuredWidth)
                        : source.Scale;
                    drawLines.Add(new DrawLine(text, source.Color, drawScale,
                        i == wrapped.Count - 1 ? 1f : 0f));
                }
            }

            float glyphHeight = font.MeasureString("A").Y;
            float titleY = 4f;
            float titleHeight = glyphHeight * drawTitleScale;
            float dividerY = titleY + titleHeight + 2f;
            float bodyY = dividerY + (drawLines.Count > 0 ? 4f : 0f);
            float panelHeight = bodyY + 4f;
            foreach (DrawLine line in drawLines) {
                panelHeight += glyphHeight * line.Scale + 2f + line.GapAfter;
            }

            Rectangle panel = PlacePanel(cursor, contentWidth + PanelPaddingX * 2f, panelHeight);
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            //贴身投影 + 暗水玻璃底 + 顶缘水线
            sb.Draw(pixel, new Rectangle(panel.X + 2, panel.Y + 3, panel.Width, panel.Height), src,
                Color.Black * (alpha * 0.45f));
            sb.Draw(pixel, panel, src, KikasaHudTheme.Void(rain) * (alpha * 0.95f));
            KikasaVaults.KikasaVaultRenderer.DrawLine(sb,
                new Vector2(panel.X + 2, panel.Y), new Vector2(panel.Right - 2, panel.Y),
                1.4f, KikasaHudTheme.Glow(rain) * (alpha * 0.55f));

            //题下一线泡沫分隔
            if (drawLines.Count > 0) {
                KikasaVaults.KikasaVaultRenderer.DrawLine(sb,
                    new Vector2(panel.X + 5f, panel.Y + dividerY),
                    new Vector2(panel.Right - 5f, panel.Y + dividerY),
                    1f, KikasaHudTheme.Accent(rain) * (alpha * 0.5f));
            }

            Utils.DrawBorderString(sb, title, new Vector2(panel.X + PanelPaddingX, panel.Y + titleY),
                KikasaHudTheme.Text(rain) * alpha, drawTitleScale);
            float y = panel.Y + bodyY;
            foreach (DrawLine line in drawLines) {
                Utils.DrawBorderString(sb, line.Text, new Vector2(panel.X + PanelPaddingX, y),
                    line.Color * alpha, line.Scale);
                y += glyphHeight * line.Scale + 2f + line.GapAfter;
            }
        }

        private static Rectangle PlacePanel(Vector2 cursor, float requestedWidth, float requestedHeight) {
            int screenWidth = Math.Max(1, (int)MathF.Floor(KikasaHudTheme.UIScreenW));
            int screenHeight = Math.Max(1, (int)MathF.Floor(KikasaHudTheme.UIScreenH));
            int width = Math.Min(Math.Max(1, (int)MathF.Ceiling(requestedWidth)),
                Math.Max(1, screenWidth - ScreenPadding * 2));
            int height = Math.Min(Math.Max(1, (int)MathF.Ceiling(requestedHeight)),
                Math.Max(1, screenHeight - ScreenPadding * 2));

            int x = (int)cursor.X + 16;
            if (x + width > screenWidth - ScreenPadding) {
                x = (int)cursor.X - width - 12;
            }
            int y = (int)cursor.Y - 6;
            if (y + height > screenHeight - ScreenPadding) {
                y = (int)cursor.Y - height - 12;
            }

            int maxX = Math.Max(ScreenPadding, screenWidth - width - ScreenPadding);
            int maxY = Math.Max(ScreenPadding, screenHeight - height - ScreenPadding);
            x = Math.Clamp(x, ScreenPadding, maxX);
            y = Math.Clamp(y, ScreenPadding, maxY);
            return new Rectangle(x, y, width, height);
        }
    }

    /// <summary>压在其余 UIHandle 之上的鬼伞 HUD 悬浮说明层</summary>
    internal sealed class KikasaHudTipOverlay : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.Vanilla_Mouse_Text;
        public override float RenderPriority => 10f;
        public override bool Active => KikasaHud.Instance?.Active ?? false;

        public override void Draw(SpriteBatch spriteBatch)
            => KikasaHud.Instance?.DrawTooltipOverlay(spriteBatch);
    }
}
