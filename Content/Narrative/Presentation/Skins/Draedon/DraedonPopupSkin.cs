using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using InnoVault.Narrative.Presentation.Popups;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Draedon
{
    internal sealed class DraedonPopupSkin : StoryPopupSkin
    {
        private readonly DraedonPanelState state = new() {
            TechSideMargin = 18f,
            DataSpawnInterval = 25,
            MaxDataParticles = 10,
            CircuitSpawnInterval = 42,
            MaxCircuitNodes = 4,
            ParticleInsetY = 40f
        };

        public override Color TitleColor => new(220, 245, 255);
        public override Color BodyColor => new(170, 230, 250);
        public override Color HintColor => new(0, 210, 185);

        public override void Update(PopupLayoutContext context)
            => state.Update(context.PanelRect, context.Alpha > 0.04f);

        public override void Reset() => state.Reset();

        public override void DrawPanel(SpriteBatch spriteBatch, PopupLayoutContext context)
            => DraedonPanelDraw.DrawPanel(spriteBatch, context.PanelRect, context.Alpha, state,
                DraedonPanelDetail.Full, shadowLayers: 6);

        public override void DrawParticles(SpriteBatch spriteBatch, PopupLayoutContext context)
            => state.DrawParticles(spriteBatch, context.Alpha, 0.85f, 0.75f);

        public override void DrawTitle(SpriteBatch spriteBatch, PopupLayoutContext context) {
            if (string.IsNullOrEmpty(context.Title)) {
                return;
            }

            float contentAlpha = MathHelper.Clamp(context.ContentAppear, 0f, 1f) * context.Alpha;
            Vector2 size = context.Font.MeasureString(context.Title) * 0.8f;
            Vector2 pos = new(context.TitleRect.Center.X - size.X / 2f, context.TitleRect.Y);
            DraedonPanelDraw.DrawSpeakerGlow(spriteBatch, pos, context.Title, contentAlpha, 0.8f);
            Utils.DrawBorderString(spriteBatch, context.Title, pos, TitleColor * contentAlpha, 0.8f);
        }
    }
}
