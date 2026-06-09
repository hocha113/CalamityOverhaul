namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>
    /// 人体工学枪托：大幅降低法力消耗，提升攻速与精准，持久输出的核心托架
    /// </summary>
    internal sealed class ErgonomicStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //柔和米白
        public override Color TintColor => new(230, 220, 180);

        public override void Apply(ref ShootContext ctx) {
            ctx.ManaCostMul += -0.3f;
            ctx.AttackSpeedMul += 0.06f;
            ctx.SpreadMul += -0.1f;
        }
    }
}
