using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using InnoVault.Narrative.Presentation.Choices;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Kikasa
{
    /// <summary>选项是水面浮签,hover = 涟漪溅开 + 波光扫入</summary>
    internal sealed class KikasaChoiceSkin : StoryChoiceSkin
    {
        private readonly KikasaPanelState _state = new();

        /// <summary>伞章列宽,文字让位</summary>
        private const float GlyphColumn = 26f;

        public override Color TextColor => KikasaPanelState.Text;
        public override Color DisabledTextColor => new(96, 112, 118);
        public override Color HighlightColor => KikasaPanelState.Moon;
        public override float OptionHeight => 34f;

        public override void Update(ChoiceLayoutContext context) {
            _state.Update(context.PanelRect, context.Alpha > 0.01f, KikasaParticleMode.Choice);
            _state.UpdateOptionHovers(context.HoverIndex, context.Options.Count);
        }

        public override void Reset() => _state.Reset();

        public override void DrawPanel(SpriteBatch spriteBatch, ChoiceLayoutContext context)
            => KikasaPanelDraw.DrawShaderBackground(spriteBatch, context.PanelRect, context.Alpha, _state);

        public override void DrawTitle(SpriteBatch spriteBatch, ChoiceLayoutContext context) {
            DrawTitleDecoration(spriteBatch, context);
            string title = ResolveChoiceTitle();
            Vector2 pos = context.TitleRect.Location.ToVector2();
            //标题前小伞章
            KikasaPanelDraw.DrawUmbrellaGlyph(spriteBatch, pos + new Vector2(7f, 10f), 12f, context.Alpha);
            Utils.DrawBorderString(spriteBatch, title, pos + new Vector2(18f, 0f), KikasaPanelState.Moon * context.Alpha, 0.85f);
        }

        public override void DrawDivider(SpriteBatch spriteBatch, ChoiceLayoutContext context) {
            Vector2 start = new(context.DividerRect.X, context.DividerRect.Y);
            Vector2 end = new(context.DividerRect.Right, context.DividerRect.Y);
            KikasaPanelDraw.DrawWaterline(spriteBatch, start, end, 1.2f, context.Alpha * 0.80f, _state.SwayTimer * 2f);
        }

        public override void DrawOptionBackground(SpriteBatch spriteBatch, ChoiceLayoutContext context, ChoiceOptionPresentation option, Rectangle rect, int optionIndex, float hover) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            float ease = option.Enabled ? _state.GetOptionHover(optionIndex) : 0f;

            //暗色浮签
            Color slip = option.Enabled
                ? Color.Lerp(KikasaPanelState.Deep * 0.38f, KikasaPanelState.Mid * 0.55f, ease)
                : KikasaPanelState.Deep * 0.16f;
            spriteBatch.Draw(pixel, rect, src, slip * context.Alpha);

            //左端签头受光边
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), src,
                (option.Enabled ? KikasaPanelState.Moon : KikasaPanelState.Deep) * (context.Alpha * (option.Enabled ? 0.16f : 0.25f)));

            if (!option.Enabled) {
                Common.SkinDrawUtil.DrawRectBorder(spriteBatch, rect, new Color(46, 60, 64) * (context.Alpha * 0.30f), 1);
                return;
            }

            if (ease > 0.02f) {
                Vector2 glyphCenter = new(rect.X + 13f, rect.Center.Y);

                //落签溅起的涟漪:hover 过渡中段最亮,张开即散;移开时只余轻微回波
                float splash = MathHelper.Clamp(ease * (1f - ease) * 4f, 0f, 1f) * (hover > 0.5f ? 1f : 0.35f);
                float rippleR = MathHelper.Lerp(4f, 17f, 1f - (1f - ease) * (1f - ease));
                KikasaPanelDraw.DrawRippleRing(spriteBatch, glyphCenter, rippleR, context.Alpha * splash * 0.75f);

                //伞章浮现
                float pop = VaultUtils.EaseOutBack(Math.Min(1f, ease));
                KikasaPanelDraw.DrawUmbrellaGlyph(spriteBatch, glyphCenter, 14f * pop, context.Alpha * ease, (1f - ease) * 0.22f);

                //波光下缘扫入
                float sweep = 1f - (float)Math.Pow(1f - ease, 3);
                KikasaPanelDraw.DrawWaterline(spriteBatch,
                    new Vector2(rect.X + 4f, rect.Bottom - 2f), new Vector2(rect.Right - 4f, rect.Bottom - 2f),
                    1.0f, context.Alpha * ease, _state.SwayTimer * 2.4f + optionIndex * 1.7f, sweep);
            }
        }

        public override void DrawOptionText(SpriteBatch spriteBatch, ChoiceLayoutContext context, ChoiceOptionPresentation option, Rectangle rect, int optionIndex, float hover) {
            string text = option.Enabled || string.IsNullOrEmpty(option.DisabledHint) ? option.Text : $"{option.Text} ({option.DisabledHint})";
            float ease = option.Enabled ? _state.GetOptionHover(optionIndex) : 0f;
            Vector2 textPos = new(rect.X + GlyphColumn, rect.Center.Y - context.Font.MeasureString(text).Y * TextScale / 2f);

            if (ease > 0.02f) {
                Color glow = KikasaPanelState.Moon * (context.Alpha * ease * 0.38f);
                for (int i = 0; i < 4; i++) {
                    float ang = MathHelper.TwoPi * i / 4f;
                    Utils.DrawBorderString(spriteBatch, text, textPos + ang.ToRotationVector2() * 1.1f, glow, TextScale);
                }
            }

            Color col = option.Enabled
                ? Color.Lerp(TextColor, KikasaPanelState.WetInk, ease * 0.85f)
                : DisabledTextColor;
            Utils.DrawBorderString(spriteBatch, text, textPos, col * context.Alpha, TextScale);
        }
    }
}
