namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>
    /// 延伸枪托：大幅延长光束射程与初速，配合狙击瞄具可实现超远程打击
    /// </summary>
    internal sealed class ExtenderStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //延伸银灰
        public override Color TintColor => new(190, 200, 210);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamLifeMul += 0.65f;
            ctx.BeamSpeedMul += 0.3f;
            ctx.DamageMul += -0.05f;
        }
    }
}
