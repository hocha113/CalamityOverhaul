namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>后坐枪托，球爆反推玩家</summary>
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
