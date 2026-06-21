using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.Narrative.Presentation
{
    public interface INarrativePanelAnchor
    {
        float ShowProgress { get; }
        Rectangle GetPanelRect();
    }
}
