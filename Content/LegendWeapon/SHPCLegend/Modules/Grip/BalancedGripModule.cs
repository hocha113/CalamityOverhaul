namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    internal sealed class BalancedGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //平衡青铜
        public override Color TintColor => new(220, 180, 120);

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -0.16f;
            ctx.AttackSpeedMul += 0.04f;
            ctx.DamageMul += 0.02f;
        }
    }
}
