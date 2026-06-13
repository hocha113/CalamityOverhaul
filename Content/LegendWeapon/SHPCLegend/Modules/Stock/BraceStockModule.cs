namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>支架枪托：零散布+延长射程，攻速略减</summary>
    internal sealed class BraceStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //精准钢银
        public override Color TintColor => new(160, 185, 210);

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -1f;
            ctx.BeamSpeedMul += 0.5f;
            ctx.BeamLifeMul += 0.5f;
            ctx.AttackSpeedMul += -0.20f;
        }
    }
}
