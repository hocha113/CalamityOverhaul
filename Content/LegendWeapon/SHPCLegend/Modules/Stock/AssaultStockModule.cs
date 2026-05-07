namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    internal sealed class AssaultStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //突击橙红
        public override Color TintColor => new(255, 100, 60);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += 0.05f;
            ctx.AttackSpeedMul += 0.10f;
            ctx.ManaCostMul += 0.5f;
        }
    }
}
