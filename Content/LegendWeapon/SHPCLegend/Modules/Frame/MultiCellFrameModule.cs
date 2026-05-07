namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    internal sealed class MultiCellFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //多重荧绿
        public override Color TintColor => new(100, 255, 80);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamCountAdd += 2;
            ctx.DamageMul += -0.2f;
            ctx.SpreadMul += 0.4f;
        }
    }
}
