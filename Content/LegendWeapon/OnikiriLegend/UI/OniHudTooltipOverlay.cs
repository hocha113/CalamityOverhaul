using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    internal readonly struct OniTooltipLine(string text, Color color, float scale = 0.7f)
    {
        public readonly string Text = text;
        public readonly Color Color = color;
        public readonly float Scale = scale;
    }

    /// <summary>鬼切悬浮说明的实测排版与四边约束</summary>
    internal static class OniTooltipPanel
    {
        private const int ScreenPadding = 8;
        private const float PanelPaddingX = 10f;
        private const float MaxPanelWidth = 420f;

        private readonly struct DrawLine(string text, Color color, float scale, float gapAfter)
        {
            public readonly string Text = text;
            public readonly Color Color = color;
            public readonly float Scale = scale;
            public readonly float GapAfter = gapAfter;
        }

        public static void Draw(SpriteBatch sb, Vector2 cursor, string title, float titleScale,
            float alpha, params OniTooltipLine[] body) {
            if (alpha <= 0.02f || string.IsNullOrEmpty(title)) {
                return;
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float availablePanelWidth = Math.Max(1f, OnikiriUITheme.UIScreenW - ScreenPadding * 2f);
            float panelWidthLimit = Math.Min(MaxPanelWidth, availablePanelWidth);
            float contentWidthLimit = Math.Max(1f, panelWidthLimit - PanelPaddingX * 2f);
            float naturalWidth = font.MeasureString(title).X * titleScale;
            foreach (OniTooltipLine line in body) {
                if (!string.IsNullOrEmpty(line.Text)) {
                    naturalWidth = Math.Max(naturalWidth, font.MeasureString(line.Text).X * line.Scale);
                }
            }

            float minContentWidth = Math.Min(72f, contentWidthLimit);
            float contentWidth = MathHelper.Clamp(naturalWidth, minContentWidth, contentWidthLimit);
            float measuredTitleWidth = font.MeasureString(title).X;
            float drawTitleScale = measuredTitleWidth > 0f
                ? Math.Min(titleScale, contentWidth / measuredTitleWidth)
                : titleScale;

            List<DrawLine> drawLines = [];
            foreach (OniTooltipLine source in body) {
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

            float maxPanelHeight = Math.Max(1f, OnikiriUITheme.UIScreenH - ScreenPadding * 2f);
            float glyphHeight = font.MeasureString("A").Y;
            float maxTitleScale = Math.Max(0.1f, (maxPanelHeight - 7f) / glyphHeight);
            drawTitleScale = Math.Min(drawTitleScale, maxTitleScale);
            float titleY = 3f;
            float titleHeight = glyphHeight * drawTitleScale;
            float dividerY = titleY + titleHeight + 1f;
            float bodyY = dividerY + (drawLines.Count > 0 ? 3f : 0f);
            FitLinesToHeight(drawLines, font, contentWidth,
                Math.Max(0f, maxPanelHeight - bodyY - 3f), glyphHeight);
            bodyY = dividerY + (drawLines.Count > 0 ? 3f : 0f);
            float panelHeight = bodyY + 3f;
            foreach (DrawLine line in drawLines) {
                panelHeight += glyphHeight * line.Scale + 2f + line.GapAfter;
            }

            Rectangle panel = PlacePanel(cursor, contentWidth + PanelPaddingX * 2f, panelHeight);
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            sb.Draw(pixel, new Rectangle(panel.X + 2, panel.Y + 3, panel.Width, panel.Height), src,
                new Color(8, 2, 5) * (alpha * 0.5f));
            sb.Draw(pixel, panel, src, OnikiriUITheme.Ink * (alpha * 0.95f));

            if (drawLines.Count > 0) {
                OniBrush.DrawTaperedSlash(sb, new Vector2(panel.X + 4f, panel.Y + dividerY),
                    new Vector2(panel.Right - 4f, panel.Y + dividerY - 1f), 1.3f, 0.7f, alpha * 0.7f);
            }

            Utils.DrawBorderString(sb, title, new Vector2(panel.X + PanelPaddingX, panel.Y + titleY),
                OnikiriUITheme.HotWhite * alpha, drawTitleScale);
            float y = panel.Y + bodyY;
            foreach (DrawLine line in drawLines) {
                Utils.DrawBorderString(sb, line.Text, new Vector2(panel.X + PanelPaddingX, y),
                    line.Color * alpha, line.Scale);
                y += glyphHeight * line.Scale + 2f + line.GapAfter;
            }
        }

        private static void FitLinesToHeight(List<DrawLine> lines, DynamicSpriteFont font,
            float contentWidth, float availableHeight, float glyphHeight) {
            float usedHeight = 0f;
            int visibleCount = 0;
            while (visibleCount < lines.Count) {
                DrawLine line = lines[visibleCount];
                float lineHeight = glyphHeight * line.Scale + 2f + line.GapAfter;
                if (usedHeight + lineHeight > availableHeight) {
                    break;
                }
                usedHeight += lineHeight;
                visibleCount++;
            }

            if (visibleCount >= lines.Count) {
                return;
            }
            if (visibleCount <= 0) {
                lines.Clear();
                return;
            }

            DrawLine last = lines[visibleCount - 1];
            lines.RemoveRange(visibleCount, lines.Count - visibleCount);
            lines[^1] = new DrawLine(AddEllipsis(last.Text, font, contentWidth, last.Scale),
                last.Color, last.Scale, 0f);
        }

        private static string AddEllipsis(string text, DynamicSpriteFont font,
            float contentWidth, float scale) {
            const string suffix = "...";
            float unscaledWidth = scale > 0f ? contentWidth / scale : contentWidth;
            string trimmed = text.TrimEnd();
            while (trimmed.Length > 0 && font.MeasureString(trimmed + suffix).X > unscaledWidth) {
                trimmed = trimmed[..^1].TrimEnd();
            }
            return trimmed + suffix;
        }

        private static Rectangle PlacePanel(Vector2 cursor, float requestedWidth, float requestedHeight) {
            int screenWidth = Math.Max(1, (int)MathF.Floor(OnikiriUITheme.UIScreenW));
            int screenHeight = Math.Max(1, (int)MathF.Floor(OnikiriUITheme.UIScreenH));
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

    /// <summary>压在其余 UIHandle 之上的鬼切 HUD 悬浮说明层</summary>
    internal sealed class OniHudTooltipOverlay : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.Vanilla_Mouse_Text;
        public override float RenderPriority => 10f;
        public override bool Active => OniTalismanHud.Instance?.Active ?? false;

        public override void Draw(SpriteBatch spriteBatch)
            => OniTalismanHud.Instance?.DrawTooltipOverlay(spriteBatch);
    }
}
