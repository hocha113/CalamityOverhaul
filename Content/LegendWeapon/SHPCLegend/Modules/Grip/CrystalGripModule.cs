namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    internal sealed class CrystalGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //水晶幻紫
        public override Color TintColor => new(200, 130, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.ManaCostMul += -0.16f;
            ctx.CritAdd += 4;
            ctx.ChargeTimeMul += 0.12f;
        }
    }
}
