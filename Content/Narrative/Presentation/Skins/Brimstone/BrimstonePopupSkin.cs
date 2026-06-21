using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;

using InnoVault.Narrative.Presentation.Popups;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Terraria;



namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Brimstone

{

    internal sealed class BrimstonePopupSkin : StoryPopupSkin

    {

        private readonly BrimstonePanelState _state = new();

        private float _hoverGlow;



        public override Color TitleColor => new(255, 225, 210);

        public override Color BodyColor => new(255, 190, 160);

        public override Color HintColor => new(255, 160, 90);



        public override void Update(PopupLayoutContext context) {

            _hoverGlow = context.State?.Hover == true ? 0.15f : 0f;

            _state.Update(context.PanelRect, context.Alpha > 0.01f, BrimstoneParticleMode.Popup, context.Alpha);

        }



        public override void Reset() {

            _state.Reset();

            _hoverGlow = 0f;

        }



        public override void DrawPanel(SpriteBatch spriteBatch, PopupLayoutContext context)

            => BrimstonePanelDraw.DrawShaderBackground(spriteBatch, context.PanelRect, context.Alpha, _state, _hoverGlow);



        public override void DrawFrame(SpriteBatch spriteBatch, PopupLayoutContext context)

            => BrimstonePanelDraw.DrawPopupFrame(spriteBatch, context.PanelRect, context.Alpha, _hoverGlow, _state);



        public override void DrawParticles(SpriteBatch spriteBatch, PopupLayoutContext context)

            => _state.DrawParticles(spriteBatch, context.Alpha);



        public override void DrawTitle(SpriteBatch spriteBatch, PopupLayoutContext context) {

            if (string.IsNullOrEmpty(context.Title)) {

                return;

            }



            float contentAlpha = MathHelper.Clamp(context.ContentAppear, 0f, 1f) * context.Alpha;

            Vector2 size = context.Font.MeasureString(context.Title) * 0.8f;

            Vector2 pos = new(context.TitleRect.Center.X - size.X / 2f, context.TitleRect.Y);

            Color nameGlow = new Color(255, 150, 80) * (contentAlpha * 0.6f);

            for (int i = 0; i < 4; i++) {

                float ang = MathHelper.TwoPi * i / 4f;

                Utils.DrawBorderString(spriteBatch, context.Title, pos + ang.ToRotationVector2() * 1.7f, nameGlow * 0.55f, 0.8f);

            }

            Utils.DrawBorderString(spriteBatch, context.Title, pos, TitleColor * contentAlpha, 0.8f);

        }

    }

}


