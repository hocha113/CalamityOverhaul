using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using InnoVault.Narrative.Presentation;
using InnoVault.Narrative.Presentation.Choices;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Draedon
{
    internal sealed class DraedonChoiceSkin : StoryChoiceSkin
    {
        private readonly DraedonPanelState state = new() {
            TechSideMargin = 22f,
            DataSpawnInterval = 28,
            MaxDataParticles = 8,
            CircuitSpawnInterval = 36,
            MaxCircuitNodes = 5,
            ParticleInsetY = 30f
        };

        public override Color TextColor => new(220, 245, 255);
        public override Color DisabledTextColor => new(55, 75, 85);
        public override Color HighlightColor => new(0, 220, 210);

        public override void Update(ChoiceLayoutContext context)
            => state.Update(context.PanelRect, context.Alpha > 0.04f);

        public override void Reset() => state.Reset();

        public override void DrawPanel(SpriteBatch spriteBatch, ChoiceLayoutContext context)
            => DraedonPanelDraw.DrawPanel(spriteBatch, context.PanelRect, context.Alpha, state, shadowLayers: 8);

        public override void DrawBackgroundDecorations(SpriteBatch spriteBatch, ChoiceLayoutContext context)
            => state.DrawParticles(spriteBatch, context.Alpha, 0.72f, 0.62f);

        public override void DrawTitleDecoration(SpriteBatch spriteBatch, ChoiceLayoutContext context)
            => DraedonPanelDraw.DrawSpeakerGlow(spriteBatch, context.TitleRect.Location.ToVector2(),
                NarrativeUIText.ChoiceTitle, context.Alpha, 0.95f);

        public override void DrawDivider(SpriteBatch spriteBatch, ChoiceLayoutContext context) {
            Vector2 start = new(context.DividerRect.X, context.DividerRect.Y);
            Vector2 end = new(context.DividerRect.Right, context.DividerRect.Y);
            DraedonPanelDraw.DrawDashDivider(spriteBatch, start, end, context.Alpha, state.DataStreamTimer);
        }

        public override void DrawOptionBackground(SpriteBatch spriteBatch, ChoiceLayoutContext context,
            ChoiceOptionPresentation option, Rectangle rect, int optionIndex, float hover) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color choiceBg = option.Enabled
                ? Color.Lerp(new Color(4, 14, 22) * 0.3f, new Color(10, 32, 38) * 0.55f, hover)
                : new Color(8, 10, 14) * 0.15f;
            spriteBatch.Draw(px, rect, new Rectangle(0, 0, 1, 1), choiceBg * context.Alpha);

            Color techColor = DraedonPanelDraw.GetEdgeColor(context.Alpha, state.HologramFlicker);
            if (option.Enabled && hover > 0.01f) {
                DraedonPanelDraw.DrawChoiceBorder(spriteBatch, rect, techColor * (hover * 0.6f));
                DraedonPanelDraw.DrawChoiceDashIndicator(spriteBatch, rect, techColor, hover, context.Alpha, state.DataStreamTimer);
            }
            else if (!option.Enabled) {
                DraedonPanelDraw.DrawChoiceBorder(spriteBatch, rect, new Color(0, 55, 65) * (context.Alpha * 0.2f));
            }
        }

        public override void DrawOptionText(SpriteBatch spriteBatch, ChoiceLayoutContext context,
            ChoiceOptionPresentation option, Rectangle rect, int optionIndex, float hover) {
            string text = GetOptionDisplayText(option);
            Vector2 textPos = new(rect.X + 8f, rect.Center.Y - context.Font.MeasureString(text).Y * TextScale / 2f);
            if (option.Enabled && hover > 0.01f) {
                Color glow = DraedonPanelDraw.GetEdgeColor(context.Alpha, state.HologramFlicker) * (hover * 0.65f);
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.TwoPi * i / 4f;
                    Utils.DrawBorderString(spriteBatch, text, textPos + angle.ToRotationVector2(), glow * 0.45f, TextScale);
                }
            }
            Utils.DrawBorderString(spriteBatch, text, textPos,
                (option.Enabled ? TextColor : DisabledTextColor) * context.Alpha, TextScale);
        }
    }
}
