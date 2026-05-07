namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>
    /// 支架枪托：彻底消除散布，延长光束飞行距离，牺牲一定攻速换取高度精准
    /// 纯 Apply 改件，不依赖钩子
    /// </summary>
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
