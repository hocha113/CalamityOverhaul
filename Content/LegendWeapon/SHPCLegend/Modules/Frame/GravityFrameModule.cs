namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>机匣：能量球蓄力时持续吸引附近敌人，爆炸范围扩大</summary>
    internal sealed class GravityFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //引力深紫
        public override Color TintColor => new(90, 60, 200);

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbDrainAura = true;
            ctx.OrbExplosionRadiusMul += 0.50f;
            ctx.ChargeTimeMul += 0.2f;
        }
    }
}
