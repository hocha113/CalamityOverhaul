namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    internal sealed class KineticDamperModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //减震橄榄绿
        public override Color TintColor => new(140, 180, 90);

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -0.5f;
            ctx.AttackSpeedMul += -0.08f;
            ctx.CritAdd += 3;
        }
    }
}
