namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>棱镜激光枪管，左键换持续跟光标光柱，线段伤不耗穿</summary>
    internal sealed class LaserBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //紫光
        public override Color TintColor => new(160, 80, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.LaserMode = true;
            ctx.DamageMul += -0.24f;
            ctx.ManaCostMul += 0.9f;
        }
    }
}
