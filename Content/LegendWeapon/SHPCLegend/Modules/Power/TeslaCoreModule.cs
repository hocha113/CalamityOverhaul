namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>能源：光束命中后向最近的敌人弹跳两次</summary>
    internal sealed class TeslaCoreModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //特斯拉电弧蓝白
        public override Color TintColor => new(120, 220, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamChainCount += 2;
            ctx.BeamChainRange = 280f;
            ctx.BeamExtraPierce += 1;
            ctx.ManaCostMul += 0.15f;
        }
    }
}
