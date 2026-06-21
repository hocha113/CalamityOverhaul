using CalamityOverhaul.Common;
using InnoVault.Narrative.Presentation.Dialogue;
using InnoVault.Narrative.Runtime;
using ReLogic.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation
{
    /// <summary>修正 Narrative 对话折行：TextRect 已是屏幕像素宽，WrapText 内部再除 TextScale。</summary>
    internal static class StoryDialogueLayoutUtil
    {
        internal static void RefreshWrappedLines(DialogueLayoutContext layout, LinePresentation line) {
            if (line == null || layout.TextRect.Width <= 0) {
                return;
            }

            DynamicSpriteFont font = layout.Font ?? Terraria.GameContent.FontAssets.MouseText.Value;
            float width = Math.Max(60f, layout.TextRect.Width);
            layout.WrappedLines = CWRUtils.WrapText(line.Text ?? string.Empty, font, width, layout.TextScale).ToArray();

            int total = 0;
            foreach (string wrappedLine in layout.WrappedLines) {
                total += wrappedLine.Length;
            }

            line.TotalChars = total;
            line.LayoutReady = true;
            layout.TotalChars = total;
            layout.VisibleChars = Math.Clamp(line.VisibleCharCount, 0, total);
        }
    }
}
