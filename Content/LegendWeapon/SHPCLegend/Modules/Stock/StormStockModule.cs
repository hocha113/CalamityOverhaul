namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>
    /// 风暴枪托：高攻速多弹幕全屏覆盖，单发伤害大幅折损，量取胜
    /// 配合霰射枪管可达到弹幕地毯覆盖效果
    /// </summary>
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
