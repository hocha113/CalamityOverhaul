namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    internal sealed class CapacitorBankModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //储能黄绿
        public override Color TintColor => new(200, 255, 80);

        public override void Apply(ref ShootContext ctx) {
            ctx.ChargeTimeMul += -0.32f;
            ctx.OrbSpeedMul += -0.12f;
            ctx.AttackSpeedMul += -0.06f;
        }
    }
}
