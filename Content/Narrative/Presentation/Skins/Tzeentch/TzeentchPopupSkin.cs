using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using InnoVault.Narrative.Presentation.Popups;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Tzeentch
{
    internal sealed class TzeentchPopupSkin : StoryPopupSkin
    {
        private readonly TzeentchPanelState _state = new();
        private float _hoverGlow;

        protected override Color Fill => TzeentchPalette.Deep;
        protected override Color Edge => new(178, 138, 232);

        public override Color TitleColor => new(245, 230, 255);
        public override Color BodyColor => new(214, 200, 245);

        public override void Update(PopupLayoutContext context) {
            _hoverGlow = context.State?.Hover == true ? 0.15f : 0f;
            _state.Update(context.PanelRect, context.Alpha > 0.01f);
        }

        public override void Reset() {
            _state.Reset();
            _hoverGlow = 0f;
        }

        public override void DrawPanel(SpriteBatch spriteBatch, PopupLayoutContext context)
            => TzeentchPanelDraw.DrawShaderBackground(spriteBatch, context.PanelRect, context.Alpha, _state, _hoverGlow);

        public override void DrawFrame(SpriteBatch spriteBatch, PopupLayoutContext context) {
            float alpha = context.Alpha * (0.9f + _hoverGlow * 0.4f);
            TzeentchPanelDraw.DrawCornerSigils(spriteBatch, context.PanelRect, alpha);
        }

        public override void DrawParticles(SpriteBatch spriteBatch, PopupLayoutContext context)
            => _state.DrawForeground(spriteBatch, context.Alpha);
    }
}
