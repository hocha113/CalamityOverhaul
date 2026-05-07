namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    internal sealed class EfficientGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //高效翠绿
        public override Color TintColor => new(60, 220, 120);

        public override void Apply(ref ShootContext ctx) {
            ctx.ManaCostMul += -0.15f;
            ctx.AttackSpeedMul += 0.08f;
        }
    }
}
