namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    internal sealed class HeavyBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //重型炮管赤红
        public override Color TintColor => new(220, 40, 60);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += 0.45f;
            ctx.AttackSpeedMul += -0.65f;
            ctx.SpreadMul += -0.5f;
        }
    }
}
