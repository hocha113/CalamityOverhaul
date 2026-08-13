using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>动量枪托，移动叠层，停下衰减，注入攻速/弹速</summary>
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
            //层数注入
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

        private void SpawnStackVFX(Player player) {
            if (Main.netMode == NetmodeID.Server) return;
            if (player.whoAmI != Main.myPlayer) return;
            Vector2 dirOpp = -player.velocity.SafeNormalize(Vector2.Zero);
            float grade = _stacks / (float)MaxStacks;
            bool maxed = _stacks >= MaxStacks;
            //尾流甩尾，速度拉伸火花沿反速度撇出，层越高焰尾越长越亮
            int sparks = maxed ? 5 : 3;
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = dirOpp.RotatedBy(Main.rand.NextFloat(-0.35f, 0.35f))
                    * Main.rand.NextFloat(3.5f, 6.5f + grade * 3f) + player.velocity * 0.2f;
                PRTLoader.NewParticle<PRT_Spark>(player.Center + Main.rand.NextVector2Circular(6f, 10f), vel,
                    Color.Lerp(new Color(255, 180, 80), new Color(255, 120, 40), Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.6f, 1f + grade * 0.3f)).Configure(false, Main.rand.Next(12, 20));
            }
            //满层提速拍，小环外弹
            if (maxed) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center, Vector2.Zero,
                    new Color(255, 170, 70), 0.05f).Configure(0.05f, 0.3f, 12);
            }
        }
    }
}
