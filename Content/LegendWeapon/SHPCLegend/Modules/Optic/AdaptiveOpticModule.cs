namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    internal sealed class AdaptiveOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //智能跟踪洋红
        public override Color TintColor => new(255, 70, 200);

        public override void Apply(ref ShootContext ctx) {
            ctx.HomingMul += 0.4f;
            ctx.AttackSpeedMul += 0.04f;
            ctx.CritAdd += 4;
        }
    }
}
