namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    internal sealed class ResonanceFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //共振翡翠
        public override Color TintColor => new(80, 255, 160);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamCountAdd += 1;
        }
    }
}
