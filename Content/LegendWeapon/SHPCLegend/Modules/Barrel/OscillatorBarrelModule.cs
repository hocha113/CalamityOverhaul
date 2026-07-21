namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>震荡激光枪管，每30帧终点脉冲，经 LaserPulseInterval</summary>
    internal sealed class OscillatorBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //脉冲橙
        public override Color TintColor => new(255, 140, 0);

        public override void Apply(ref ShootContext ctx) {
            ctx.LaserMode = true;
            ctx.LaserPulseInterval = 30;
            ctx.LaserPulseRadius = 85f;
            ctx.DamageMul += -0.36f;
            ctx.ManaCostMul += 0.72f;
        }
    }
}
