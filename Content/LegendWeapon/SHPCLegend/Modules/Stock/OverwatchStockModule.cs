using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>
    /// 守望枪托：玩家速度极低（接近静止）时逐帧累积"哨戒精度"层数，移动则迅速衰减
    /// 层数会反过来动态注入到 <see cref="ShootContext"/>，因此 tooltip 会实时反映当前层数贡献
    /// </summary>
    internal sealed class OverwatchStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //哨戒蓝白
        public override Color TintColor => new(150, 220, 255);

        private const int MaxStacks = 5;
        private const float StationaryThreshold = 0.6f;
        private const int StackUpInterval = 18;
        private const float DamagePerStack = 0.03f;
        private const float CritPerStack = 2f;

        private int _stacks;
        private int _stackUpTimer;

        public override void Apply(ref ShootContext ctx) {
            //基础属性：精确但攻速略减
            ctx.SpreadMul += -0.35f;
            ctx.AttackSpeedMul += -0.1f;
            //当前层数动态注入
            ctx.DamageMul += _stacks * DamagePerStack;
            ctx.CritAdd += (int)(_stacks * CritPerStack);
        }

        public override void OnPlayerUpdate(Player player) {
            if (player == null || !player.active) return;
            float speed = player.velocity.Length();
            if (speed < StationaryThreshold) {
                _stackUpTimer++;
                if (_stackUpTimer >= StackUpInterval && _stacks < MaxStacks) {
                    _stackUpTimer = 0;
                    _stacks++;
                    SpawnStackVFX(player);
                }
            }
            else {
                _stackUpTimer = 0;
                if (_stacks > 0 && Main.GameUpdateCount % 6 == 0) {
                    _stacks--;
                }
            }
        }

        private static void SpawnStackVFX(Player player) {
            if (Main.netMode == Terraria.ID.NetmodeID.Server) return;
            if (player.whoAmI != Main.myPlayer) return;
            //简短的提示粒子，避免长期堆积
            for (int i = 0; i < 6; i++) {
                Vector2 angle = MathHelper.TwoPi * i / 6f * Vector2.One;
                Vector2 vel = (MathHelper.TwoPi * i / 6f).ToRotationVector2() * 2.5f;
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    player.Center + new Vector2(0, -player.height * 0.5f), vel,
                    new Color(180, 230, 255), new Color(100, 170, 230),
                    Main.rand.NextFloat(0.6f, 1.0f), 14));
            }
        }
    }
}
