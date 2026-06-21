using CalamityOverhaul.Content.UIs.StorageUIs;

namespace CalamityOverhaul.Content.Narrative.Scenarios.OldDuke.OldDuchests.OldDuchestUIs
{
    /// <summary>
    /// 老箱子UI动画 - 木质主题
    /// </summary>
    internal class OldDuchestAnimation : BaseChestAnimation
    {
        public float WoodGrainPhase { get; private set; } = 0f;
        public float DustTimer { get; private set; } = 0f;
        public float GlowTimer { get; private set; } = 0f;

        public OldDuchestAnimation() : base(240) { }

        public override void UpdateThemeEffects() {
            WoodGrainPhase += 0.01f;
            DustTimer += 0.025f;
            GlowTimer += 0.018f;

            if (WoodGrainPhase > MathHelper.TwoPi) WoodGrainPhase -= MathHelper.TwoPi;
            if (DustTimer > MathHelper.TwoPi) DustTimer -= MathHelper.TwoPi;
            if (GlowTimer > MathHelper.TwoPi) GlowTimer -= MathHelper.TwoPi;
        }

        public override void Reset() {
            base.Reset();
            WoodGrainPhase = 0f;
            DustTimer = 0f;
            GlowTimer = 0f;
        }
    }
}
