using InnoVault.Narrative.Core;
using InnoVault.Narrative.Presentation.Anchors;
using InnoVault.Narrative.Presentation.Popups;

namespace CalamityOverhaul.Content.Narrative.Presentation.Views
{
    internal sealed class RewardPopupView : NarrativePopupViewBase<RewardPopupView>
    {
        protected override Vector2 ResolvePopupAnchor(PopupPayload payload) {
            float gap = payload is { AnchorGap: > 0f } ? payload.AnchorGap : 70f;
            Vector2 anchor = PanelAnchorResolver.AboveDialogue(gap);
            if (payload?.AnchorYOffset != 0f) {
                anchor.Y += payload.AnchorYOffset;
            }
            return anchor;
        }
    }
}
