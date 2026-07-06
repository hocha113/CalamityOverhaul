using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using InnoVault.Narrative.Presentation.Choices;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Onikiri
{
    /// <summary>
    /// 鬼切选择框:御神签式暗色签条。左端常驻朱印列(hover 时印章砸落),
    /// 签条下缘随 hover 扫入一道刀痕。抉择时刻不落花,保持静场
    /// </summary>
    internal sealed class OnikiriChoiceSkin : StoryChoiceSkin
    {
        private readonly OnikiriPanelState _state = new();

        /// <summary>朱印列宽:选项文字统一让位,避免盖章时文字跳动</summary>
        private const float SealColumn = 26f;

        public override Color TextColor => OnikiriPanelState.Paper;
        public override Color DisabledTextColor => new(128, 92, 86);
        public override Color HighlightColor => OnikiriPanelState.Bright;
        public override float OptionHeight => 34f;

        public override void Update(ChoiceLayoutContext context) {
            _state.Update(context.PanelRect, context.Alpha > 0.01f, OnikiriParticleMode.Choice);
            _state.UpdateOptionHovers(context.HoverIndex, context.Options.Count);
        }

        public override void Reset() => _state.Reset();

        public override void DrawPanel(SpriteBatch spriteBatch, ChoiceLayoutContext context)
            => OnikiriPanelDraw.DrawShaderBackground(spriteBatch, context.PanelRect, context.Alpha, _state);

        public override void DrawTitle(SpriteBatch spriteBatch, ChoiceLayoutContext context) {
            DrawTitleDecoration(spriteBatch, context);
            string title = ResolveChoiceTitle();
            Vector2 pos = context.TitleRect.Location.ToVector2();
            //标题前一枚小印,标题字用白热色
            OnikiriPanelDraw.DrawSealGlyph(spriteBatch, pos + new Vector2(6f, 10f), 11f, context.Alpha);
            Utils.DrawBorderString(spriteBatch, title, pos + new Vector2(17f, 0f), OnikiriPanelState.HotWhite * context.Alpha, 0.85f);
        }

        public override void DrawDivider(SpriteBatch spriteBatch, ChoiceLayoutContext context) {
            Vector2 start = new(context.DividerRect.X, context.DividerRect.Y);
            Vector2 end = new(context.DividerRect.Right, context.DividerRect.Y);
            OnikiriPanelDraw.DrawTaperedSlash(spriteBatch, start, end, 1.9f, 1.3f, context.Alpha * 0.85f);
        }

        public override void DrawOptionBackground(SpriteBatch spriteBatch, ChoiceLayoutContext context, ChoiceOptionPresentation option, Rectangle rect, int optionIndex, float hover) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            float ease = option.Enabled ? _state.GetOptionHover(optionIndex) : 0f;

            //暗色签条:纸感靠形与左端印列,而非亮底(维持全 mod 暗底纪律与正负对比一致)
            Color slip = option.Enabled
                ? Color.Lerp(OnikiriPanelState.Dark * 0.34f, OnikiriPanelState.Deep * 0.46f, ease)
                : OnikiriPanelState.Dark * 0.14f;
            spriteBatch.Draw(pixel, rect, src, slip * context.Alpha);

            //签条左端的浅色签头切角提示(常驻,极淡)
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), src,
                (option.Enabled ? OnikiriPanelState.Paper : OnikiriPanelState.Dark) * (context.Alpha * (option.Enabled ? 0.18f : 0.25f)));

            if (!option.Enabled) {
                Common.SkinDrawUtil.DrawRectBorder(spriteBatch, rect, new Color(78, 38, 34) * (context.Alpha * 0.28f), 1);
                return;
            }

            if (ease > 0.02f) {
                //朱印砸落:EaseOutBack 弹入
                float pop = VaultUtils.EaseOutBack(Math.Min(1f, ease));
                OnikiriPanelDraw.DrawSealGlyph(spriteBatch,
                    new Vector2(rect.X + 13f, rect.Center.Y), 14f * pop, context.Alpha * ease, (1f - ease) * 0.25f);

                //刀痕沿签条下缘扫入
                float sweep = 1f - (float)Math.Pow(1f - ease, 3);
                OnikiriPanelDraw.DrawTaperedSlash(spriteBatch,
                    new Vector2(rect.X + 4f, rect.Bottom - 2f), new Vector2(rect.Right - 4f, rect.Bottom - 2f),
                    1.7f, 1.0f, context.Alpha * ease, sweep);
            }
        }

        public override void DrawOptionText(SpriteBatch spriteBatch, ChoiceLayoutContext context, ChoiceOptionPresentation option, Rectangle rect, int optionIndex, float hover) {
            string text = option.Enabled || string.IsNullOrEmpty(option.DisabledHint) ? option.Text : $"{option.Text} ({option.DisabledHint})";
            float ease = option.Enabled ? _state.GetOptionHover(optionIndex) : 0f;
            Vector2 textPos = new(rect.X + SealColumn, rect.Center.Y - context.Font.MeasureString(text).Y * TextScale / 2f);

            if (ease > 0.02f) {
                Color glow = OnikiriPanelState.Bright * (context.Alpha * ease * 0.4f);
                for (int i = 0; i < 4; i++) {
                    float ang = MathHelper.TwoPi * i / 4f;
                    Utils.DrawBorderString(spriteBatch, text, textPos + ang.ToRotationVector2() * 1.1f, glow, TextScale);
                }
            }

            Color col = option.Enabled
                ? Color.Lerp(TextColor, OnikiriPanelState.HotWhite, ease)
                : DisabledTextColor;
            Utils.DrawBorderString(spriteBatch, text, textPos, col * context.Alpha, TextScale);
        }
    }
}
