namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>奇点核心，球飞行偏转追踪最近敌人（OrbFlyingAttract）</summary>
    internal sealed class SingularityCoreModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //奇点深紫
        public override Color TintColor => new(140, 0, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbFlyingAttract = true;
            ctx.OrbSpeedMul += 0.16f;
            ctx.OrbExplosionRadiusMul += 0.24f;
            ctx.ManaCostMul += 0.6f;
        }
    }
}
