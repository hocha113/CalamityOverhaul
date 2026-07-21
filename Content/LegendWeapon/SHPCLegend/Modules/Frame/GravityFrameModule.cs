namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>引力机匣，蓄力球持续吸敌，爆炸范围放大</summary>
    internal sealed class GravityFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //引力深紫
        public override Color TintColor => new(90, 60, 200);

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbDrainAura = true;
            ctx.OrbExplosionRadiusMul += 0.4f;
            ctx.ChargeTimeMul += 0.24f;
        }
    }
}
