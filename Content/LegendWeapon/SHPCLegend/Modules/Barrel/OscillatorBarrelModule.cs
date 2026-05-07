namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 震荡激光枪管：每45帧在光束终点引爆一次脉冲爆炸
    /// 通过 ShootContext.LaserPulseInterval 字段传递给 CyberPrismLaserProj 消费
    /// </summary>
    internal sealed class OscillatorBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //震荡脉冲橙
        public override Color TintColor => new(255, 140, 0);

        public override void Apply(ref ShootContext ctx) {
            ctx.LaserMode = true;
            ctx.LaserPulseInterval = 30;
            ctx.LaserPulseRadius = 85f;
            ctx.DamageMul += -0.3f;
            ctx.ManaCostMul += 0.6f;
        }
    }
}
