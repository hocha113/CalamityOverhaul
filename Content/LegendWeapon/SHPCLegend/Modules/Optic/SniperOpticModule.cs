namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>
    /// 狙击瞄具：大幅提升弹速与射程，牺牲追踪能力与攻速
    /// 配合超音速枪管形成超远程单点打击
    /// </summary>
    internal sealed class SniperOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //狙击冷白
        public override Color TintColor => new(220, 240, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamSpeedMul += 1f;
            ctx.BeamLifeMul += 0.8f;
            ctx.DamageMul += 0.3f;
            ctx.AttackSpeedMul += -0.45f;
            ctx.HomingMul += -0.9f;
            ctx.SpreadMul += -1f;
        }
    }
}
