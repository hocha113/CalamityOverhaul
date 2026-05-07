namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    internal sealed class ScattershotBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //霰射狂暴的橙色调
        public override Color TintColor => new(255, 130, 30);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamCountAdd += 2;
            ctx.SpreadMul += 1.2f;
            ctx.DamageMul += -0.3f;
        }
    }
}
