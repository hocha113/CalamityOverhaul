namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>棱镜瞄具，消亡分裂 2 道副束</summary>
    internal sealed class PrismOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //棱镜彩光
        public override Color TintColor => new(190, 110, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamSplitOnDeath += 2;
            //原束略短命换暴击
            ctx.BeamLifeMul += -0.24f;
            ctx.CritAdd += 4;
        }
    }
}
