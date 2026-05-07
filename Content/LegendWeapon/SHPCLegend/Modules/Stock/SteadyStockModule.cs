namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    internal sealed class SteadyStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //沉稳金属灰
        public override Color TintColor => new(180, 200, 220);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += -0.2f;
            ctx.DamageMul += 0.12f;
        }
    }
}
