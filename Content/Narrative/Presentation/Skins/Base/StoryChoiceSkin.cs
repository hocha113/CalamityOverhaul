using InnoVault.Narrative.Styling;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Base
{
    internal class StoryChoiceSkin : ChoiceSkin
    {
        protected virtual Color Fill => new(14, 20, 32);
        protected virtual Color Edge => new(70, 130, 200);

        public override Color HighlightColor => Edge;

        protected override string ResolveChoiceTitle() => DialogueSystem.ChoiceTitle.Value;

        public override void DrawPanel(SpriteBatch spriteBatch, Rectangle panel, float alpha)
            => NarrativeSkinDraw.DrawPanel(spriteBatch, panel, Fill, Edge, alpha);
    }
}
