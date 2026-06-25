using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using InnoVault.Narrative.Presentation.Choices;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Sulfsea
{
    internal sealed class SulfseaChoiceSkin : StoryChoiceSkin
    {
        private readonly SulfseaPanelState _state = new();

        protected override Color Fill => new(12, 18, 8);
        protected override Color Edge => new(120, 150, 60);

        public override Color TextColor => new(235, 240, 210);
        public override Color DisabledTextColor => new(90, 105, 65);

        public override void Update(ChoiceLayoutContext context) => _state.Update(context.PanelRect, context.Alpha > 0.01f, includeStars: false);

        public override void Reset() => _state.Reset();

        public override void DrawPanel(SpriteBatch spriteBatch, ChoiceLayoutContext context) {
            SulfseaPanelDraw.DrawShaderBackground(spriteBatch, context.PanelRect, context.Alpha, _state);
            float pulse = (float)Math.Sin(_state.SulfurPulse * 2.2f) * 0.5f + 0.5f;
            SulfseaPanelDraw.DrawFrame(spriteBatch, context.PanelRect, context.Alpha, pulse);
        }

        public override void DrawBackgroundDecorations(SpriteBatch spriteBatch, ChoiceLayoutContext context)
            => _state.DrawForeground(spriteBatch, context.Alpha);

        public override void DrawTitleDecoration(SpriteBatch spriteBatch, ChoiceLayoutContext context) {
            Color glow = new Color(160, 190, 80) * (context.Alpha * 0.75f);
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
                Edge * (context.Alpha * 0.9f), Edge * (context.Alpha * 0.08f), 1.3f);

        public override void DrawOptionBackground(SpriteBatch spriteBatch, ChoiceLayoutContext context, ChoiceOptionPresentation option, Rectangle rect, int optionIndex, float hover) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color choiceBg = option.Enabled
                ? Color.Lerp(new Color(20, 30, 10) * 0.3f, new Color(50, 70, 25) * 0.5f, hover)
                : new Color(15, 20, 10) * 0.15f;
            spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), choiceBg * context.Alpha);
            Color border = option.Enabled ? Edge * (context.Alpha * (0.25f + hover * 0.55f)) : new Color(60, 80, 35) * (context.Alpha * 0.25f);
            SkinDrawUtil.DrawRectBorder(spriteBatch, rect, border, 1);
            if (option.Enabled && hover > 0.01f) {
                float toxicGlow = (float)Math.Sin(_state.ToxicWavePhase * 2f + hover * 3f) * 0.5f + 0.5f;
                spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), new Color(100, 140, 50) * (context.Alpha * 0.15f * hover * toxicGlow));
            }
        }

        public override void DrawOptionText(SpriteBatch spriteBatch, ChoiceLayoutContext context, ChoiceOptionPresentation option, Rectangle rect, int optionIndex, float hover) {
            string text = option.Enabled || string.IsNullOrEmpty(option.DisabledHint) ? option.Text : $"{option.Text} ({option.DisabledHint})";
            Vector2 textPos = new(rect.X + 8f, rect.Center.Y - context.Font.MeasureString(text).Y * TextScale / 2f);
            if (option.Enabled && hover > 0.01f) {
                Color glow = new Color(160, 190, 80) * (context.Alpha * hover * 0.65f);
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.TwoPi * i / 4f;
                    Utils.DrawBorderString(spriteBatch, text, textPos + angle.ToRotationVector2(), glow * 0.45f, TextScale);
                }
            }
            Utils.DrawBorderString(spriteBatch, text, textPos, (option.Enabled ? TextColor : DisabledTextColor) * context.Alpha, TextScale);
        }
    }
}
