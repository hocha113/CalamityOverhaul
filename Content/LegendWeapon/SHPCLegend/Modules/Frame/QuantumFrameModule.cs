namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    internal sealed class QuantumFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //量子超紫
        public override Color TintColor => new(140, 80, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.HomingMul += 0.32f;
            ctx.OrbSpeedMul += 0.32f;
            ctx.ManaCostMul += 0.2f;
        }
    }
}
