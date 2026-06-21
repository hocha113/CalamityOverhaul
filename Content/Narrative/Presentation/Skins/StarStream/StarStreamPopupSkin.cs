using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using InnoVault.Narrative.Presentation;
using InnoVault.Narrative.Presentation.Popups;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.StarStream
{
    internal sealed class StarStreamPopupSkin : StoryPopupSkin
    {
        private readonly StarStreamPanelState state = new();

        public override Color TitleColor => new(255, 245, 220);
        public override Color BodyColor => new(245, 220, 170);
        public override Color HintColor => new(255, 210, 100);

        public override void Update(PopupLayoutContext context) {
            float hoverGlow = context.State?.Hover == true ? 0.15f : 0f;
            state.UpdatePopup(context.PanelRect, context.Alpha > 0.01f, context.Alpha + hoverGlow);
        }

        public override void Reset() => state.Reset();

        public override void DrawPanel(SpriteBatch spriteBatch, PopupLayoutContext context) {
            float hoverGlow = context.State?.Hover == true ? 0.15f : 0f;
            StarStreamPanelDraw.DrawPopupBackground(spriteBatch, context.PanelRect, context.Alpha, hoverGlow, state);
        }

        public override void DrawFrame(SpriteBatch spriteBatch, PopupLayoutContext context) {
            float hoverGlow = context.State?.Hover == true ? 0.15f : 0f;
            StarStreamPanelDraw.DrawPopupFrame(spriteBatch, context.PanelRect, context.Alpha, hoverGlow, state);
        }

        public override void DrawParticles(SpriteBatch spriteBatch, PopupLayoutContext context)
            => state.DrawStreamParticles(spriteBatch, context.Alpha);

        public override void DrawTitle(SpriteBatch spriteBatch, PopupLayoutContext context) {
            if (string.IsNullOrEmpty(context.Title)) {
                return;
            }

            Vector2 size = context.Font.MeasureString(context.Title) * 0.8f;
            Vector2 pos = new(context.TitleRect.Center.X - size.X / 2f, context.TitleRect.Y);

            Color nameGlow = new Color(255, 210, 120) * context.Alpha * 0.7f;
            for (int i = 0; i < 5; i++) {
                float angle = MathHelper.TwoPi * i / 5f + state.ShimmerTimer * 0.3f;
                Utils.DrawBorderString(spriteBatch, context.Title, pos + angle.ToRotationVector2() * 2f, nameGlow * 0.5f, 0.8f);
            }

            Utils.DrawBorderString(spriteBatch, context.Title, pos, TitleColor * context.Alpha, 0.8f);
        }

        public override void DrawHint(SpriteBatch spriteBatch, PopupLayoutContext context) {
            string hint = context.RequireClaim ? ResolveClaimHint() : ResolveContinueHint();
            Vector2 hintSize = context.Font.MeasureString(hint) * 0.6f;
            float blink = (float)(Math.Sin(context.GlobalTimer * 6f) * 0.5 + 0.5);
            Utils.DrawBorderString(spriteBatch, hint,
                new Vector2(context.HintRect.Right - hintSize.X, context.HintRect.Bottom - hintSize.Y),
                HintColor * (context.Alpha * blink), 0.6f);
        }
    }
}
