using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>壁垒枪托：持 SHPC 时少量减伤，PostUpdate 叠 endurance</summary>
    internal sealed class BulwarkStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //壁垒钢蓝
        public override Color TintColor => new(120, 160, 200);

        private const float EnduranceBoost = 0.06f;

        public override void Apply(ref ShootContext ctx) {
            ctx.ChargeTimeMul += 0.15f;
            ctx.DamageMul += -0.05f;
            ctx.ManaCostMul += -0.10f;
        }

        public override void OnPlayerUpdate(Player player) {
            if (player == null || !player.active) return;
            //仅在持有 SHPC 时启用减伤；endurance 由 vanilla 每帧重置，无需手动清理
            if (player.HeldItem == null || player.HeldItem.type != SHPCOverride.ID) return;
            player.endurance += EnduranceBoost;
        }
    }
}
