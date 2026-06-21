using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using InnoVault.Narrative.Presentation.Popups;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Sea
{
    internal sealed class SeaPopupSkin : StoryPopupSkin
    {
        private readonly SeaPanelState _state = new();
        private float _hoverGlow;

        public override Color TitleColor => new(210, 245, 255);
        public override Color BodyColor => new(170, 220, 240);
        public override Color HintColor => new(140, 230, 255);

        public override void Update(PopupLayoutContext context) {
            _hoverGlow = context.State?.Hover == true ? 0.12f : 0f;
            _state.Update(context.PanelRect, context.Alpha > 0.01f, popupMode: true, panelAlpha: context.Alpha);
        }

        public override void Reset() {
            _state.Reset();
            _hoverGlow = 0f;
        }

        public override void DrawPanel(SpriteBatch spriteBatch, PopupLayoutContext context)
            => SeaPanelDraw.DrawShaderBackground(spriteBatch, context.PanelRect, context.Alpha, _state, _hoverGlow);

        public override void DrawFrame(SpriteBatch spriteBatch, PopupLayoutContext context)
            => SeaPanelDraw.DrawPopupFrame(spriteBatch, context.PanelRect, context.Alpha, _hoverGlow);

        public override void DrawParticles(SpriteBatch spriteBatch, PopupLayoutContext context)
            => _state.DrawForeground(spriteBatch, context.Alpha);

        public override void DrawTitle(SpriteBatch spriteBatch, PopupLayoutContext context) {
            if (string.IsNullOrEmpty(context.Title)) {
                return;
            }

            float contentAlpha = MathHelper.Clamp(context.ContentAppear, 0f, 1f) * context.Alpha;
            Vector2 size = context.Font.MeasureString(context.Title) * 0.8f;
            Vector2 pos = new(context.TitleRect.Center.X - size.X / 2f, context.TitleRect.Y);
            Color nameGlow = new Color(140, 230, 255) * (contentAlpha * 0.6f);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f;
                Utils.DrawBorderString(spriteBatch, context.Title, pos + ang.ToRotationVector2() * 1.7f, nameGlow * 0.55f, 0.8f);
            }
            Utils.DrawBorderString(spriteBatch, context.Title, pos, TitleColor * contentAlpha, 0.8f);
        }
    }
}
