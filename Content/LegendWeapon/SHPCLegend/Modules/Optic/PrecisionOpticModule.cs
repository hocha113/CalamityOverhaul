namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    internal sealed class PrecisionOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //精确蓝绿色
        public override Color TintColor => new(80, 255, 200);

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -1.0f;
            ctx.CritAdd += 8;
        }
    }
}
