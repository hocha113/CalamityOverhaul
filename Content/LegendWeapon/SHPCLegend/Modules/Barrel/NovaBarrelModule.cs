namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>新星枪管，命中引爆微型脉冲</summary>
    internal sealed class NovaBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //橘红
        public override Color TintColor => new(255, 110, 50);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamExplodeOnHit = true;
            ctx.BeamExplodeRadius = 60f;
            //高散布+蓝耗
            ctx.SpreadMul += 0.6f;
            ctx.DamageMul += -0.36f;
            ctx.ManaCostMul += 0.75f;
            //弹越多爆伤越低，每发-25%
            ctx.BeamExplodeDecayPerBeam = 0.25f;
        }
    }
}
