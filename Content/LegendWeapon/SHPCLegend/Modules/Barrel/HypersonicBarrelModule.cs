namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    internal sealed class HypersonicBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //超音速主题黄色
        public override Color TintColor => new(255, 235, 60);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamSpeedMul += 0.88f;
            ctx.AttackSpeedMul += 0.16f;
            ctx.DamageMul += -0.12f;
            ctx.HomingMul += -0.84f;
        }
    }
}
