using InnoVault.Narrative.Styling;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Base
{
    internal class StoryPopupSkin : PopupSkin
    {
        protected virtual Color Fill => new(16, 24, 36);
        protected virtual Color Edge => new(80, 150, 210);

        public override Color HintColor => Edge;

        protected override string ResolveClaimHint() => DialogueSystem.ClaimHint.Value;

        protected override string ResolveContinueHint() => DialogueSystem.PopupContinueHint.Value;

        public override void DrawPanel(SpriteBatch spriteBatch, Rectangle panel, float alpha)
            => NarrativeSkinDraw.DrawPanel(spriteBatch, panel, Fill, Edge, alpha);
    }
}
