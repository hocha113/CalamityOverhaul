namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    internal sealed class PlasmaInjectorModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //等离子注入粉紫
        public override Color TintColor => new(255, 100, 220);

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbSpeedMul += 0.75f;
            ctx.MergedDamageBonus += 0.5f;
            ctx.ChargeTimeMul += 0.3f;
        }
    }
}
