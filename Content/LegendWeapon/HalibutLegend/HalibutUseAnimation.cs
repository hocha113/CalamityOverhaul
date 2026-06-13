using InnoVault.GameSystem;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutUseAnimation : AimedHoldAnimation
    {
        public override int TargetID => HalibutOverride.ID;
        public override float HoldDistance => 7f;
        public override Vector2 HoldOrigin => new Vector2(-40, 6);
        public override float SwingStrength => 0.06f;
        public override float SwingPhase => 0.4f;
    }
}
