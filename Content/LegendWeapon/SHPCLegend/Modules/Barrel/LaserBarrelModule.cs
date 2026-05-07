namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 棱镜激光枪管，将左键攻击模式完全替换为持续跟随光标的激光光柱
    /// 激光通过线段碰撞持续伤害，不消耗穿透次数
    /// </summary>
    internal sealed class LaserBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //棱镜紫光
        public override Color TintColor => new(160, 80, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.LaserMode = true;
            ctx.DamageMul += -0.2f;
            ctx.ManaCostMul += 0.75f;
        }
    }
}
