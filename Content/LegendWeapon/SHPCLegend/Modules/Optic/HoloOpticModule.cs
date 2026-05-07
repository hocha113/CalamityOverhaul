namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    internal sealed class HoloOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //全息投影湖蓝
        public override Color TintColor => new(50, 200, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -0.55f;
            ctx.AttackSpeedMul += 0.12f;
            ctx.ManaCostMul += 0.15f;
        }
    }
}
