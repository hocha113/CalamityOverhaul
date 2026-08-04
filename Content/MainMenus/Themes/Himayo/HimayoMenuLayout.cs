using ReLogic.Graphics;
using System;
using Terraria.Localization;

namespace CalamityOverhaul.Content.MainMenus.Themes.Himayo
{
    internal readonly struct HimayoMenuButtonLayout
    {
        internal readonly HimayoMenuAction Action;
        internal readonly string Text;
        internal readonly Rectangle HitBox;
        internal readonly Vector2 TextPosition;
        internal readonly Vector2 TextSize;
        internal readonly float TextScale;
        internal readonly bool Primary;

        internal HimayoMenuButtonLayout(HimayoMenuAction action, string text, Rectangle hitBox,
            Vector2 textPosition, Vector2 textSize, float textScale, bool primary) {
            Action = action;
            Text = text;
            HitBox = hitBox;
            TextPosition = textPosition;
            TextSize = textSize;
            TextScale = textScale;
            Primary = primary;
        }
    }

    internal sealed class HimayoMenuLayout
    {
        internal const int ButtonCount = 7;

        private readonly HimayoMenuButtonLayout[] buttons = new HimayoMenuButtonLayout[ButtonCount];

        internal ReadOnlySpan<HimayoMenuButtonLayout> Buttons => buttons;

        internal Vector2 PlaquePosition { get; private set; }

        internal Rectangle ThemeSwitchRect { get; private set; }

        internal Rectangle PreviousThemeRect { get; private set; }

        internal Rectangle NextThemeRect { get; private set; }

        internal void Rebuild(DynamicSpriteFont font) {
            float width = HimayoMenuInput.UIScreenWidth;
            float height = HimayoMenuInput.UIScreenHeight;
            float anchorX = width * 0.275f;
            float startY = height * 0.30f;
            float availableHeight = Math.Max(220f, height - startY - 82f);
            float rowScale = MathHelper.Clamp(availableHeight / 322f, 0.72f, 1f);
            float y = startY;

            PlaquePosition = new Vector2(anchorX, Math.Max(34f, startY - 73f * rowScale));

            for (int i = 0; i < ButtonCount; i++) {
                bool primary = i < 2;
                float rowHeight = (primary ? 56f : 42f) * rowScale;
                float textScale = (primary ? 1.18f : 0.92f) * rowScale;
                string text = GetButtonText(i);
                Vector2 measured = font.MeasureString(text) * textScale;
                Vector2 textPosition = new Vector2(anchorX - measured.X * 0.5f, y + (rowHeight - measured.Y) * 0.5f);
                float hitWidth = Math.Max(measured.X + 42f, 154f * rowScale);
                Rectangle hitBox = new Rectangle(
                    (int)(anchorX - hitWidth * 0.5f),
                    (int)y,
                    (int)hitWidth,
                    Math.Max(26, (int)rowHeight));

                buttons[i] = new HimayoMenuButtonLayout((HimayoMenuAction)i, text, hitBox,
                    textPosition, measured, textScale, primary);
                y += rowHeight;
            }

            int switchWidth = Math.Min(226, Math.Max(174, (int)width / 7));
            int switchHeight = 30;
            ThemeSwitchRect = new Rectangle(
                ((int)width - switchWidth) / 2,
                (int)height - switchHeight - 10,
                switchWidth,
                switchHeight);
            PreviousThemeRect = new Rectangle(ThemeSwitchRect.X, ThemeSwitchRect.Y, 42, switchHeight);
            NextThemeRect = new Rectangle(ThemeSwitchRect.Right - 42, ThemeSwitchRect.Y, 42, switchHeight);
        }

        internal bool ContainsMenuControl(Point point) {
            if (ThemeSwitchRect.Contains(point)) {
                return true;
            }

            for (int i = 0; i < buttons.Length; i++) {
                if (buttons[i].HitBox.Contains(point)) {
                    return true;
                }
            }
            return false;
        }

        private static string GetButtonText(int index) {
            return index switch {
                0 => Language.GetTextValue("LegacyMenu.12"),
                1 => Language.GetTextValue("LegacyMenu.13"),
                2 => Language.GetTextValue("LegacyMenu.131"),
                3 => Language.GetTextValue("UI.Workshop"),
                4 => Language.GetTextValue("LegacyMenu.14"),
                5 => Language.GetTextValue("UI.Credits"),
                6 => Language.GetTextValue("LegacyMenu.15"),
                _ => string.Empty
            };
        }
    }
}
