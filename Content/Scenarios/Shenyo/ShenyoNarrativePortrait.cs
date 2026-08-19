using CalamityOverhaul.Content.Narrative.Presentation.Views;
using System;

namespace CalamityOverhaul.Content.Scenarios.Shenyo
{
    internal static class ShenyoNarrativePortrait
    {
        public static void Show(ShenyoFullBodyPortrait.Face face = ShenyoFullBodyPortrait.Face.None, bool skipFadeIn = true) {
            DialoguePanelView.Instance?.ShowFullBodyPortrait<ShenyoFullBodyPortrait>();
            if (DialoguePanelView.Instance?.GetActiveFullBodyPortrait() is not ShenyoFullBodyPortrait portrait) {
                return;
            }

            if (skipFadeIn) {
                portrait.SkipFadeIn();
            }

            portrait.currentFace = face;
        }

        /// <summary>黑雨汇聚入场：初始闭目，睁眼交给台本换脸</summary>
        public static void ShowRainAssembly(ShenyoFullBodyPortrait.Face face = ShenyoFullBodyPortrait.Face.None) {
            Show(face);
            if (DialoguePanelView.Instance?.GetActiveFullBodyPortrait() is ShenyoFullBodyPortrait portrait) {
                portrait.StartRainAssembly();
            }
        }

        public static void SetFace(ShenyoFullBodyPortrait.Face face) {
            if (DialoguePanelView.Instance?.GetActiveFullBodyPortrait() is ShenyoFullBodyPortrait portrait) {
                portrait.currentFace = face;
            }
        }

        public static Action FaceEnter(ShenyoFullBodyPortrait.Face face) => () => SetFace(face);

        public static void Hide() => DialoguePanelView.Instance?.HideFullBodyPortrait();
    }
}
