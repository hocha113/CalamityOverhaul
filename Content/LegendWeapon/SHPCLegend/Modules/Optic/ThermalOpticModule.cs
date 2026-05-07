namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    internal sealed class ThermalOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //热成像火粉
        public override Color TintColor => new(255, 90, 110);

        public override void Apply(ref ShootContext ctx) {
            ctx.HomingMul += 1.5f;
            ctx.CritAdd += 4;
            ctx.SpreadMul += -0.25f;
        }
    }
}
