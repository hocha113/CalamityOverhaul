namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>枪托：能量球爆炸时反推玩家弹射</summary>
    internal sealed class RecoilStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //火药钢蓝灰
        public override Color TintColor => new(180, 180, 220);

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbExplosionPropels = true;
            ctx.BeamLifeMul += 0.30f;
            ctx.AttackSpeedMul += -0.10f;
        }
    }
}
