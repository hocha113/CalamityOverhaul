namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    internal sealed class HeavyBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //重型炮管赤红
        public override Color TintColor => new(220, 40, 60);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += 0.36f;
            ctx.AttackSpeedMul += -0.75f;
            ctx.SpreadMul += -0.4f;
        }
    }
}
