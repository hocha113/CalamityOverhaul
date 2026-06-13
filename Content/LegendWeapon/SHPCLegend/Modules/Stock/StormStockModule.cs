namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>风暴枪托：高攻速多弹，单发伤害折损</summary>
    internal sealed class StormStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //风暴红橙
        public override Color TintColor => new(255, 120, 50);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += 0.4f;
            ctx.BeamCountAdd += 1;
            ctx.DamageMul += -0.22f;
            ctx.SpreadMul += 0.5f;
        }
    }
}
