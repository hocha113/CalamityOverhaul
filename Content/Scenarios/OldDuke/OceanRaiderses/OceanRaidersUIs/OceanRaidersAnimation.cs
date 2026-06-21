using CalamityOverhaul.Content.UIs.StorageUIs;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.OceanRaiderses.OceanRaidersUIs
{
    /// <summary>
    /// 海洋吞噬者UI动画 - 硫磺海主题
    /// </summary>
    internal class OceanRaidersAnimation : BaseChestAnimation
    {
        public float ToxicWavePhase { get; private set; } = 0f;
        public float SulfurPulse { get; private set; } = 0f;
        public float MiasmaTimer { get; private set; } = 0f;
        public float BubbleTimer { get; private set; } = 0f;
        public float AcidFlowTimer { get; private set; } = 0f;

        public OceanRaidersAnimation() : base(360) { } //20x18=360格

        public override void UpdateThemeEffects() {
            ToxicWavePhase += 0.022f;
            SulfurPulse += 0.015f;
            MiasmaTimer += 0.032f;
            BubbleTimer += 0.028f;
            AcidFlowTimer += 0.018f;

            if (ToxicWavePhase > MathHelper.TwoPi) ToxicWavePhase -= MathHelper.TwoPi;
            if (SulfurPulse > MathHelper.TwoPi) SulfurPulse -= MathHelper.TwoPi;
            if (MiasmaTimer > MathHelper.TwoPi) MiasmaTimer -= MathHelper.TwoPi;
            if (BubbleTimer > MathHelper.TwoPi) BubbleTimer -= MathHelper.TwoPi;
            if (AcidFlowTimer > MathHelper.TwoPi) AcidFlowTimer -= MathHelper.TwoPi;
        }

        public override void Reset() {
            base.Reset();
            ToxicWavePhase = 0f;
            SulfurPulse = 0f;
            MiasmaTimer = 0f;
            BubbleTimer = 0f;
            AcidFlowTimer = 0f;
        }
    }
}
