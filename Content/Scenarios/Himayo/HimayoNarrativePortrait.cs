using CalamityOverhaul.Content.Narrative.Presentation.Views;
using System;

namespace CalamityOverhaul.Content.Scenarios.Himayo
{
    internal static class HimayoNarrativePortrait
    {
        public static void Show(HimayoFullBodyPortrait.Face face = HimayoFullBodyPortrait.Face.None, bool skipFadeIn = true) {
            DialoguePanelView.Instance?.ShowFullBodyPortrait<HimayoFullBodyPortrait>();
            if (DialoguePanelView.Instance?.GetActiveFullBodyPortrait() is not HimayoFullBodyPortrait portrait) {
                return;
            }

            if (skipFadeIn) {
                portrait.SkipFadeIn();
            }

            portrait.currentFace = face;
        }

        public static void ShowPetalAssembly(HimayoFullBodyPortrait.Face face = HimayoFullBodyPortrait.Face.Doubt) {
            Show(face);
            if (DialoguePanelView.Instance?.GetActiveFullBodyPortrait() is HimayoFullBodyPortrait portrait) {
                portrait.StartPetalAssembly();
            }
        }

        public static void SetFace(HimayoFullBodyPortrait.Face face) {
            if (DialoguePanelView.Instance?.GetActiveFullBodyPortrait() is HimayoFullBodyPortrait portrait) {
                portrait.currentFace = face;
            }
        }

        public static Action FaceEnter(HimayoFullBodyPortrait.Face face) => () => SetFace(face);

        public static void Hide() => DialoguePanelView.Instance?.HideFullBodyPortrait();
    }
}
