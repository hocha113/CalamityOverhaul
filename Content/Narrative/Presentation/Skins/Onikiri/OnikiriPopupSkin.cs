using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using InnoVault.Narrative.Presentation.Popups;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Onikiri
{
    internal sealed class OnikiriPopupSkin : StoryPopupSkin
    {
        private readonly OnikiriPanelState _state = new();

        private float _hoverGlow;

        public override Color TitleColor => OnikiriPanelState.HotWhite;
        public override Color BodyColor => new(238, 202, 188);
        public override Color HintColor => new(224, 122, 100);

        public override void Update(PopupLayoutContext context) {
            _hoverGlow = context.State?.Hover == true ? 1f : 0f;
            _state.Update(context.PanelRect, context.Alpha > 0.01f, OnikiriParticleMode.Popup);
        }

        public override void Reset() {
            _state.Reset();
            _hoverGlow = 0f;
        }

        public override void DrawPanel(SpriteBatch spriteBatch, PopupLayoutContext context)
            => OnikiriPanelDraw.DrawShaderBackground(spriteBatch, context.PanelRect, context.Alpha, _state);

        public override void DrawFrame(SpriteBatch spriteBatch, PopupLayoutContext context) {
            float alpha = context.Alpha;
            //挂绳流苏等展开后浮现
            float hangAlpha = MathHelper.Clamp((alpha - 0.5f) / 0.5f, 0f, 1f);
            if (hangAlpha > 0.01f) {
                OnikiriPanelDraw.DrawHangingKnot(spriteBatch, context.PanelRect, hangAlpha, _state.SwayTimer);
            }

            float pulse = (float)System.Math.Sin(_state.PulseTimer * 1.6f) * 0.5f + 0.5f;
            OnikiriPanelDraw.DrawCornerTicks(spriteBatch, context.PanelRect, alpha * (1f + _hoverGlow * 0.35f), pulse);
        }

        public override void DrawParticles(SpriteBatch spriteBatch, PopupLayoutContext context)
            => _state.DrawPetals(spriteBatch, context.Alpha);

        public override void DrawTitle(SpriteBatch spriteBatch, PopupLayoutContext context) {
            if (string.IsNullOrEmpty(context.Title)) {
                return;
            }

            float contentAlpha = MathHelper.Clamp(context.ContentAppear, 0f, 1f) * context.Alpha;
            Vector2 size = context.Font.MeasureString(context.Title) * 0.8f;
            Vector2 pos = new(context.TitleRect.Center.X - size.X / 2f, context.TitleRect.Y);
            Color glow = OnikiriPanelState.Bright * (contentAlpha * 0.4f);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f;
                Utils.DrawBorderString(spriteBatch, context.Title, pos + ang.ToRotationVector2() * 1.4f, glow, 0.8f);
            }
            Utils.DrawBorderString(spriteBatch, context.Title, pos, TitleColor * contentAlpha, 0.8f);
        }
    }
}
