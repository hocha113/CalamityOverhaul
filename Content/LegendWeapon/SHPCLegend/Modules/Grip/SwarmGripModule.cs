namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>握把：能量球爆炸时撒出迷你追踪光束</summary>
    internal sealed class SwarmGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //蜂群霓虹粉
        public override Color TintColor => new(255, 80, 180);

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbDetonationMinions += 3;
            ctx.ManaCostMul += 0.40f;
            ctx.OrbSpeedMul += -0.25f;
        }
    }
}
