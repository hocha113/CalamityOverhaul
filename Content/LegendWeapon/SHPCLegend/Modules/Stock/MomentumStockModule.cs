using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>
    /// 动量枪托：玩家移动越快累积越多"动能"层，停下后迅速衰减
    /// 与守望枪托互补，构成"站桩 vs 风筝"两套截然不同的输出曲线
    /// </summary>
    internal sealed class MomentumStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //动量电橙
        public override Color TintColor => new(255, 140, 60);

        private const int MaxStacks = 6;
        private const float MoveThreshold = 4.5f;
        private const int StackUpInterval = 14;
        private const float AttackSpeedPerStack = 0.04f;
        private const float BeamSpeedPerStack = 0.06f;

        private int _stacks;
        private int _stackUpTimer;

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.05f;
            ctx.SpreadMul += 0.25f;
            //层数动态注入
            ctx.AttackSpeedMul += _stacks * AttackSpeedPerStack;
            ctx.BeamSpeedMul += _stacks * BeamSpeedPerStack;
        }

        public override void OnPlayerUpdate(Player player) {
            if (player == null || !player.active) return;
            float speed = player.velocity.Length();
            if (speed > MoveThreshold) {
                _stackUpTimer++;
                if (_stackUpTimer >= StackUpInterval && _stacks < MaxStacks) {
                    _stackUpTimer = 0;
                    _stacks++;
                    SpawnStackVFX(player);
                }
            }
            else {
                _stackUpTimer = 0;
                if (_stacks > 0 && Main.GameUpdateCount % 4 == 0) {
                    _stacks--;
                }
            }
        }

        private static void SpawnStackVFX(Player player) {
            if (Main.netMode == Terraria.ID.NetmodeID.Server) return;
            if (player.whoAmI != Main.myPlayer) return;
            Vector2 dirOpp = -player.velocity.SafeNormalize(Vector2.Zero);
            for (int i = 0; i < 4; i++) {
                Vector2 vel = dirOpp * Main.rand.NextFloat(2f, 4f) + Main.rand.NextVector2Circular(1.2f, 1.2f);
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    player.Center, vel,
                    new Color(255, 180, 80), new Color(220, 90, 30),
                    Main.rand.NextFloat(0.7f, 1.2f), 16));
            }
        }
    }
}
