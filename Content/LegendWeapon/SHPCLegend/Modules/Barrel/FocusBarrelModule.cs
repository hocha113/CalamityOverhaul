namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    internal sealed class FocusBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //聚束高能调用电蓝
        public override Color TintColor => new(60, 130, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.MergeBeams = true;
            ctx.BeamSpeedMul += 0.6f;
            ctx.HomingMul += -0.5f;
            ctx.MergedDamageBonus += 2f;
            ctx.ManaCostMul += 0.20f;
        }
    }
}
