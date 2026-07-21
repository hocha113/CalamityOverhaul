using InnoVault.Narrative.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    /// <summary>开场Happy立绘，结束时隐藏</summary>
    internal abstract class ShepelCybCourseDialogue : NarrativeScenario, ILocalizedModType
    {
        public abstract string LocalizationCategory { get; }

        public override StyleId DefaultStyle => "SHPC";

        protected virtual bool SkipPortraitFadeInOnStart => false;

        protected override void OnStarted()
            => ShepelNarrativePortrait.Show(ShepelFullBodyPortrait.Face.Happy, SkipPortraitFadeInOnStart);

        protected override void OnCompleted() {
            ShepelNarrativePortrait.Hide();
            OnCourseCompleted();
        }

        protected virtual void OnCourseCompleted() { }
    }
}
