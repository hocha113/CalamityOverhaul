using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using InnoVault.Narrative.Presentation;
using InnoVault.Narrative.Presentation.Choices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.StarStream
{
    internal sealed class StarStreamChoiceSkin : StoryChoiceSkin
    {
        private readonly StarStreamPanelState state = new();

        public override Color TextColor => new(255, 245, 220);
        public override Color DisabledTextColor => new(120, 100, 80);
        public override Color HighlightColor => new(255, 210, 100);

        public override void Update(ChoiceLayoutContext context)
            => state.UpdateChoice(context.PanelRect, context.Alpha > 0.01f);

        public override void Reset() => state.Reset();

        public override void DrawPanel(SpriteBatch spriteBatch, ChoiceLayoutContext context)
            => StarStreamPanelDraw.DrawChoiceBackground(spriteBatch, context.PanelRect, context.Alpha, state);

        public override void DrawBackgroundDecorations(SpriteBatch spriteBatch, ChoiceLayoutContext context)
            => state.DrawDataStreams(spriteBatch, context.Alpha);

        public override void DrawTitleDecoration(SpriteBatch spriteBatch, ChoiceLayoutContext context) {
            Color nameGlow = new Color(255, 210, 120) * context.Alpha * 0.7f;
            Vector2 titlePos = context.TitleRect.Location.ToVector2();
            for (int i = 0; i < 5; i++) {
                float angle = MathHelper.TwoPi * i / 5f + state.ShimmerTimer * 0.3f;
                Utils.DrawBorderString(spriteBatch, NarrativeUIText.ChoiceTitle, titlePos + angle.ToRotationVector2() * 2.2f, nameGlow * 0.5f, 0.95f);
            }
        }

        public override void DrawDivider(SpriteBatch spriteBatch, ChoiceLayoutContext context) {
            Vector2 start = new(context.DividerRect.X, context.DividerRect.Y);
            Vector2 end = new(context.DividerRect.Right, context.DividerRect.Y);
            SkinDrawUtil.DrawGradientLine(spriteBatch, start, end,
                new Color(220, 180, 80) * (context.Alpha * 0.85f),
                new Color(220, 180, 80) * (context.Alpha * 0.06f),
                1.5f);
        }

        public override void DrawOptionBackground(SpriteBatch spriteBatch, ChoiceLayoutContext context, ChoiceOptionPresentation option, Rectangle rect, int optionIndex, float hover)
            => StarStreamPanelDraw.DrawChoiceOptionBackground(spriteBatch, rect, option.Enabled, hover, context.Alpha, state);

        public override void DrawOptionText(SpriteBatch spriteBatch, ChoiceLayoutContext context, ChoiceOptionPresentation option, Rectangle rect, int optionIndex, float hover) {
            string text = option.Enabled || string.IsNullOrEmpty(option.DisabledHint) ? option.Text : $"{option.Text} ({option.DisabledHint})";
            Vector2 textPos = new(rect.X + 8f, rect.Center.Y - context.Font.MeasureString(text).Y * TextScale / 2f);

            if (option.Enabled && hover > 0.01f) {
                Color glow = StarStreamPanelDraw.GetEdgeColor(context.Alpha, state.ShimmerTimer) * (hover * 0.65f);
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.TwoPi * i / 4f;
                    Utils.DrawBorderString(spriteBatch, text, textPos + angle.ToRotationVector2(), glow * 0.45f, TextScale);
                }
            }

            Utils.DrawBorderString(spriteBatch, text, textPos, (option.Enabled ? TextColor : DisabledTextColor) * context.Alpha, TextScale);
        }
    }
}
