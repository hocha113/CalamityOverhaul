namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>枪管：光束命中时引爆微型脉冲爆炸</summary>
    internal sealed class NovaBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //新星橘红
        public override Color TintColor => new(255, 110, 50);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamExplodeOnHit = true;
            ctx.BeamExplodeRadius = 90f;
            //爆裂枪管自带较高的散布与法力开销
            ctx.SpreadMul += 0.5f;
            ctx.DamageMul += -0.3f;
            ctx.ManaCostMul += 0.5f;
            //弹幕越多爆炸伤害越低：每多一发弹幕额外衰减25%
            ctx.BeamExplodeDecayPerBeam = 0.15f;
        }
    }
}
