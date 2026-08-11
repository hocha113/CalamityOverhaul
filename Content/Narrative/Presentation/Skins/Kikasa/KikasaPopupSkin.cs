using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using InnoVault.Narrative.Presentation.Popups;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Kikasa
{
    /// <summary>奖励自雨里递出:悬珠 + 角签水痕 + 疏雨</summary>
    internal sealed class KikasaPopupSkin : StoryPopupSkin
    {
        private readonly KikasaPanelState _state = new();

        private float _hoverGlow;

        public override Color TitleColor => KikasaPanelState.Moon;
        public override Color BodyColor => KikasaPanelState.Moon;
        public override Color HintColor => KikasaPanelState.TextDim;

        public override void Update(PopupLayoutContext context) {
            _hoverGlow = context.State?.Hover == true ? 1f : 0f;
            _state.Update(context.PanelRect, context.Alpha > 0.01f, KikasaParticleMode.Popup);
        }

        public override void Reset() {
            _state.Reset();
            _hoverGlow = 0f;
        }

        public override void DrawPanel(SpriteBatch spriteBatch, PopupLayoutContext context)
            => KikasaPanelDraw.DrawShaderBackground(spriteBatch, context.PanelRect, context.Alpha, _state);

        public override void DrawFrame(SpriteBatch spriteBatch, PopupLayoutContext context) {
            float alpha = context.Alpha;
            //悬珠等展开后浮现
            float hangAlpha = MathHelper.Clamp((alpha - 0.5f) / 0.5f, 0f, 1f);
            if (hangAlpha > 0.01f) {
                KikasaPanelDraw.DrawHangingDroplet(spriteBatch, context.PanelRect, hangAlpha, _state.SwayTimer);
            }

            float pulse = (float)System.Math.Sin(_state.PulseTimer * 1.6f) * 0.5f + 0.5f;
            KikasaPanelDraw.DrawCornerDrips(spriteBatch, context.PanelRect, alpha * (1f + _hoverGlow * 0.35f), pulse);
        }

        public override void DrawParticles(SpriteBatch spriteBatch, PopupLayoutContext context)
            => _state.DrawRain(spriteBatch, context.Alpha);

        public override void DrawTitle(SpriteBatch spriteBatch, PopupLayoutContext context) {
            if (string.IsNullOrEmpty(context.Title)) {
                return;
            }

            float contentAlpha = MathHelper.Clamp(context.ContentAppear, 0f, 1f) * context.Alpha;
            Vector2 size = context.Font.MeasureString(context.Title) * 0.8f;
            Vector2 pos = new(context.TitleRect.Center.X - size.X / 2f, context.TitleRect.Y);
            Color glow = KikasaPanelState.Moon * (contentAlpha * 0.36f);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f;
                Utils.DrawBorderString(spriteBatch, context.Title, pos + ang.ToRotationVector2() * 1.4f, glow, 0.8f);
            }
            Utils.DrawBorderString(spriteBatch, context.Title, pos, TitleColor * contentAlpha, 0.8f);
        }
    }
}
