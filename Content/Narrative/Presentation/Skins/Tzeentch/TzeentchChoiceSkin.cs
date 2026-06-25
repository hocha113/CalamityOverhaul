using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using InnoVault.Narrative.Presentation.Choices;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Tzeentch
{
    internal sealed class TzeentchChoiceSkin : StoryChoiceSkin
    {
        private readonly TzeentchPanelState _state = new();

        protected override Color Fill => TzeentchPalette.Deep;
        protected override Color Edge => new(178, 138, 232);

        public override Color TextColor => new(232, 226, 250);
        public override Color DisabledTextColor => new(96, 86, 120);

        public override void Update(ChoiceLayoutContext context) => _state.Update(context.PanelRect, context.Alpha > 0.01f, includeRunes: false);

        public override void Reset() => _state.Reset();

        public override void DrawPanel(SpriteBatch spriteBatch, ChoiceLayoutContext context) {
            TzeentchPanelDraw.DrawShaderBackground(spriteBatch, context.PanelRect, context.Alpha, _state);
            float pulse = (float)Math.Sin(_state.SchemePulse * 2.2f) * 0.5f + 0.5f;
            TzeentchPanelDraw.DrawFrame(spriteBatch, context.PanelRect, context.Alpha, pulse);
        }

        public override void DrawBackgroundDecorations(SpriteBatch spriteBatch, ChoiceLayoutContext context)
            => _state.DrawForeground(spriteBatch, context.Alpha);

        public override void DrawTitleDecoration(SpriteBatch spriteBatch, ChoiceLayoutContext context) {
            Color glow = TzeentchPalette.Gold * (context.Alpha * 0.7f);
            Vector2 titlePos = context.TitleRect.Location.ToVector2();
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Utils.DrawBorderString(spriteBatch, ResolveChoiceTitle(), titlePos + angle.ToRotationVector2() * 1.8f, glow * 0.6f, 0.85f);
            }
        }

        public override void DrawDivider(SpriteBatch spriteBatch, ChoiceLayoutContext context)
            => SkinDrawUtil.DrawGradientLine(spriteBatch,
                new Vector2(context.DividerRect.X, context.DividerRect.Y),
                new Vector2(context.DividerRect.Right, context.DividerRect.Y),
                TzeentchPalette.Gold * (context.Alpha * 0.9f), TzeentchPalette.Gold * (context.Alpha * 0.06f), 1.3f);

        public override void DrawOptionBackground(SpriteBatch spriteBatch, ChoiceLayoutContext context, ChoiceOptionPresentation option, Rectangle rect, int optionIndex, float hover) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color choiceBg = option.Enabled
                ? Color.Lerp(new Color(22, 12, 46) * 0.35f, new Color(58, 32, 96) * 0.55f, hover)
                : new Color(16, 12, 24) * 0.15f;
            spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), choiceBg * context.Alpha);

            Color border = option.Enabled
                ? Color.Lerp(Edge, TzeentchPalette.Gold, hover) * (context.Alpha * (0.28f + hover * 0.55f))
                : new Color(70, 56, 96) * (context.Alpha * 0.25f);
            SkinDrawUtil.DrawRectBorder(spriteBatch, rect, border, 1);

            if (option.Enabled && hover > 0.01f) {
                float warpGlow = (float)Math.Sin(_state.WarpTimer * 2f + hover * 3f) * 0.5f + 0.5f;
                spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), TzeentchPalette.Violet * (context.Alpha * 0.16f * hover * warpGlow));
            }
        }

        public override void DrawOptionText(SpriteBatch spriteBatch, ChoiceLayoutContext context, ChoiceOptionPresentation option, Rectangle rect, int optionIndex, float hover) {
            string text = option.Enabled || string.IsNullOrEmpty(option.DisabledHint) ? option.Text : $"{option.Text} ({option.DisabledHint})";
            Vector2 textPos = new(rect.X + 8f, rect.Center.Y - context.Font.MeasureString(text).Y * TextScale / 2f);
            if (option.Enabled && hover > 0.01f) {
                Color glow = Color.Lerp(TzeentchPalette.Violet, TzeentchPalette.Gold, hover) * (context.Alpha * hover * 0.65f);
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.TwoPi * i / 4f;
                    Utils.DrawBorderString(spriteBatch, text, textPos + angle.ToRotationVector2(), glow * 0.45f, TextScale);
                }
            }
            Utils.DrawBorderString(spriteBatch, text, textPos, (option.Enabled ? TextColor : DisabledTextColor) * context.Alpha, TextScale);
        }
    }
}
