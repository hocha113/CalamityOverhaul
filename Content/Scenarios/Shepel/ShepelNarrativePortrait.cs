using CalamityOverhaul.Content.Narrative.Presentation.Views;
using System;

namespace CalamityOverhaul.Content.Scenarios.Shepel
{
    /// <summary>Shepel Narrative 全身立绘辅助，对齐 ADV <c>ShowPortraitWithFace</c> / <c>SetPortraitFace</c>。</summary>
    internal static class ShepelNarrativePortrait
    {
        public static void Show(ShepelFullBodyPortrait.Face face = ShepelFullBodyPortrait.Face.None, bool skipFadeIn = true) {
            DialoguePanelView.Instance?.ShowFullBodyPortrait<ShepelFullBodyPortrait>();
            if (DialoguePanelView.Instance?.GetActiveFullBodyPortrait() is not ShepelFullBodyPortrait portrait) {
                return;
            }

            if (skipFadeIn) {
                portrait.SkipFadeIn();
            }

            portrait.currentFace = face;
        }

        public static void SetFace(ShepelFullBodyPortrait.Face face) {
            if (DialoguePanelView.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait) {
                portrait.currentFace = face;
            }
        }

        public static Action FaceEnter(ShepelFullBodyPortrait.Face face) => () => SetFace(face);

        public static void TriggerGlitch(float durationSeconds, float intensity)
            => Active?.TriggerGlitch(durationSeconds, intensity);

        public static ShepelFullBodyPortrait Active
            => DialoguePanelView.Instance?.GetActiveFullBodyPortrait() as ShepelFullBodyPortrait;

        public static void Hide() => DialoguePanelView.Instance?.HideFullBodyPortrait();
    }
}
