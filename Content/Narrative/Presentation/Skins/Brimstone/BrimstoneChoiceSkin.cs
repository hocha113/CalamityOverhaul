using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;

using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;

using InnoVault.Narrative.Presentation;

using InnoVault.Narrative.Presentation.Choices;

using Microsoft.Xna.Framework.Graphics;

using Terraria;



namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Brimstone

{

    internal sealed class BrimstoneChoiceSkin : StoryChoiceSkin

    {

        private readonly BrimstonePanelState _state = new();



        public override Color TextColor => new(255, 225, 210);

        public override Color DisabledTextColor => new(120, 70, 60);



        public override void Update(ChoiceLayoutContext context)

            => _state.Update(context.PanelRect, context.Alpha > 0.01f, BrimstoneParticleMode.Choice);



        public override void Reset() => _state.Reset();



        public override void DrawPanel(SpriteBatch spriteBatch, ChoiceLayoutContext context) {

            BrimstonePanelDraw.DrawShaderBackground(spriteBatch, context.PanelRect, context.Alpha, _state);

            BrimstonePanelDraw.DrawFlameBorder(spriteBatch, context.PanelRect, _state.EdgeColor(context.Alpha));

        }



        public override void DrawBackgroundDecorations(SpriteBatch spriteBatch, ChoiceLayoutContext context)

            => _state.DrawParticles(spriteBatch, context.Alpha);



        public override void DrawTitleDecoration(SpriteBatch spriteBatch, ChoiceLayoutContext context) {

            Color edge = _state.EdgeColor(context.Alpha);

            Vector2 titlePos = context.TitleRect.Location.ToVector2();

            for (int i = 0; i < 4; i++) {

                float ang = MathHelper.TwoPi * i / 4f;

                Utils.DrawBorderString(spriteBatch, ResolveChoiceTitle(), titlePos + ang.ToRotationVector2() * 1.25f, edge * 0.55f, 0.9f);

            }

        }



        public override void DrawDivider(SpriteBatch spriteBatch, ChoiceLayoutContext context) {

            Color edge = _state.EdgeColor(context.Alpha);

            SkinDrawUtil.DrawGradientLine(spriteBatch,

                new Vector2(context.DividerRect.X, context.DividerRect.Y),

                new Vector2(context.DividerRect.Right, context.DividerRect.Y),

                edge * 0.9f, edge * 0.05f, 1.3f);

        }



        public override void DrawOptionBackground(SpriteBatch spriteBatch, ChoiceLayoutContext context, ChoiceOptionPresentation option, Rectangle rect, int optionIndex, float hover) {

            Texture2D pixel = VaultAsset.placeholder2.Value;

            Color choiceBg = option.Enabled

                ? Color.Lerp(new Color(40, 10, 5) * 0.3f, new Color(100, 25, 15) * 0.5f, hover)

                : new Color(20, 10, 8) * 0.12f;

            spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), choiceBg * context.Alpha);

            if (option.Enabled && hover > 0.01f) {

                SkinDrawUtil.DrawRectBorder(spriteBatch, rect, _state.EdgeColor(context.Alpha) * (hover * 0.6f), 1);

            }

            else if (!option.Enabled) {

                SkinDrawUtil.DrawRectBorder(spriteBatch, rect, new Color(80, 40, 30) * (context.Alpha * 0.2f), 1);

            }

        }



        public override void DrawOptionText(SpriteBatch spriteBatch, ChoiceLayoutContext context, ChoiceOptionPresentation option, Rectangle rect, int optionIndex, float hover) {

            string text = option.Enabled || string.IsNullOrEmpty(option.DisabledHint) ? option.Text : $"{option.Text} ({option.DisabledHint})";

            Vector2 textPos = new(rect.X + 8f, rect.Center.Y - context.Font.MeasureString(text).Y * TextScale / 2f);

            if (option.Enabled && hover > 0.01f) {

                Color glow = _state.EdgeColor(context.Alpha) * (hover * 0.65f);

                for (int i = 0; i < 4; i++) {

                    float ang = MathHelper.TwoPi * i / 4f;

                    Utils.DrawBorderString(spriteBatch, text, textPos + ang.ToRotationVector2(), glow * 0.45f, TextScale);

                }

            }

            Utils.DrawBorderString(spriteBatch, text, textPos, (option.Enabled ? TextColor : DisabledTextColor) * context.Alpha, TextScale);

        }

    }

}


