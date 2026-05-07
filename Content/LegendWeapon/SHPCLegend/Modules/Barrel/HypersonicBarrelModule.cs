namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    internal sealed class HypersonicBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //超音速主题黄色
        public override Color TintColor => new(255, 235, 60);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamSpeedMul += 1f;
            ctx.AttackSpeedMul += 0.2f;
            ctx.DamageMul += -0.1f;
            ctx.HomingMul += -0.7f;
        }
    }
}
