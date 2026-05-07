namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>
    /// 奇点核心：能量球飞行阶段持续向最近敌人偏转追踪
    /// 通过 ShootContext.OrbFlyingAttract 字段，由 CyberChargeOrbProj 在飞行 AI 中消费
    /// </summary>
    internal sealed class SingularityCoreModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //奇点深紫
        public override Color TintColor => new(140, 0, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbFlyingAttract = true;
            ctx.OrbSpeedMul += 0.2f;
            ctx.OrbExplosionRadiusMul += 0.3f;
            ctx.ManaCostMul += 0.5f;
        }
    }
}
