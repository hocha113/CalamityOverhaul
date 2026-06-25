using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using InnoVault.Narrative.Presentation.Popups;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Sulfsea
{
    internal sealed class SulfseaPopupSkin : StoryPopupSkin
    {
        private readonly SulfseaPanelState _state = new();

        protected override Color Fill => new(12, 18, 8);
        protected override Color Edge => new(120, 150, 60);

        public override Color TitleColor => new(235, 240, 210);
        public override Color BodyColor => new(205, 220, 160);

        public override void Update(PopupLayoutContext context) {
            float hoverGlow = context.State?.Hover == true ? 0.15f : 0f;
            _state.Update(context.PanelRect, context.Alpha > 0.01f);
            _hoverGlow = hoverGlow;
        }

        private float _hoverGlow;

        public override void Reset() {
            _state.Reset();
            _hoverGlow = 0f;
        }

        public override void DrawPanel(SpriteBatch spriteBatch, PopupLayoutContext context)
            => SulfseaPanelDraw.DrawShaderBackground(spriteBatch, context.PanelRect, context.Alpha, _state, _hoverGlow);

        public override void DrawFrame(SpriteBatch spriteBatch, PopupLayoutContext context) {
            Color starTint = new(160, 190, 80);
            float alpha = context.Alpha * (0.9f + _hoverGlow * 0.4f);
            SkinDrawUtil.DrawCornerStar(spriteBatch, new Vector2(context.PanelRect.X + 10, context.PanelRect.Y + 10), alpha, starTint);
            SkinDrawUtil.DrawCornerStar(spriteBatch, new Vector2(context.PanelRect.Right - 10, context.PanelRect.Y + 10), alpha, starTint);
            SkinDrawUtil.DrawCornerStar(spriteBatch, new Vector2(context.PanelRect.X + 10, context.PanelRect.Bottom - 10), alpha * 0.72f, starTint);
            SkinDrawUtil.DrawCornerStar(spriteBatch, new Vector2(context.PanelRect.Right - 10, context.PanelRect.Bottom - 10), alpha * 0.72f, starTint);
        }

        public override void DrawParticles(SpriteBatch spriteBatch, PopupLayoutContext context)
            => _state.DrawForeground(spriteBatch, context.Alpha);
    }
}
