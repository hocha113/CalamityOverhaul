namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    internal sealed class HighVoltageCoreModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //高压电蓝
        public override Color TintColor => new(80, 180, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += 0.1f;
            ctx.MergedDamageBonus += 1f;
            ctx.ManaCostMul += 0.60f;
        }
    }
}
