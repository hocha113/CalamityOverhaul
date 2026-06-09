namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    internal sealed class OverloadCoreModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //过载电浆紫
        public override Color TintColor => new(180, 80, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbSpeedMul += 0.36f;
            ctx.ChargeTimeMul += -0.2f;
        }
    }
}
