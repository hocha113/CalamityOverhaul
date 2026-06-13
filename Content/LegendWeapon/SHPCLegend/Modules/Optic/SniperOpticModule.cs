namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>狙击瞄具：高弹速射程，低攻速与追踪</summary>
    internal sealed class SniperOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //狙击冷白
        public override Color TintColor => new(220, 240, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamSpeedMul += 1f;
            ctx.BeamLifeMul += 0.64f;
            ctx.DamageMul += 0.24f;
            ctx.AttackSpeedMul += -0.54f;
            ctx.HomingMul += -1f;
            ctx.SpreadMul += -1f;
        }
    }
}
