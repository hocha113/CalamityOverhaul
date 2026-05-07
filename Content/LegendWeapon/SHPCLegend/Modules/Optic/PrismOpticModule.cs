namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>瞄具：光束消亡时分裂出 2 道副光束</summary>
    internal sealed class PrismOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //棱镜彩光
        public override Color TintColor => new(190, 110, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamSplitOnDeath += 2;
            //分光透镜会让原始光束略微短命，但暴击爬升
            ctx.BeamLifeMul += -0.2f;
            ctx.CritAdd += 4;
        }
    }
}
