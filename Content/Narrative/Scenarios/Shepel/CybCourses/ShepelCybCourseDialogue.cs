using CalamityOverhaul.Content.Narrative.Scenarios.Shepel;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.CybCourses
{
    /// <summary>超梦教程对话基类——开场 Happy 立绘，结束时隐藏。</summary>
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
