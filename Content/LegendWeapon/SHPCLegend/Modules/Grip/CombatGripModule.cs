using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>
    /// 战斗握把：每次击杀（光束/激光命中后目标 life&lt;=0）累积"战意"层数
    /// 层数动态注入到 <see cref="ShootContext.AttackSpeedMul"/>，未触发时也不会回到中性
    /// 通过 OnPlayerUpdate 计时衰减层数，避免对外部状态产生依赖
    /// </summary>
    internal sealed class CombatGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //战意烈红
        public override Color TintColor => new(255, 80, 80);

        private const int MaxStacks = 5;
        private const float AttackSpeedPerStack = 0.04f;
        private const float DamagePerStack = 0.02f;
        private const int FreshTime = 240;
        private const int DecayInterval = 120;

        private int _stacks;
        private int _freshTimer;
        private int _decayTimer;

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -0.10f;
            ctx.ManaCostMul += 0.15f;
            //层数动态注入
            ctx.AttackSpeedMul += _stacks * AttackSpeedPerStack;
            ctx.DamageMul += _stacks * DamagePerStack;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            TryStack(beam.Projectile.owner, target);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            TryStack(laser.Projectile.owner, target);
        }

        private void TryStack(int owner, NPC target) {
            if (owner != Main.myPlayer) return;
            if (target.life > 0) return;
            if (_stacks < MaxStacks) {
                _stacks++;
                if (Main.netMode != Terraria.ID.NetmodeID.Server) {
                    Player p = Main.player[owner];
                    if (p != null && p.active) {
                        for (int i = 0; i < 5; i++) {
                            Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
                            PRTLoader.AddParticle(new PRT_CyberSquare(
                                p.Center, vel,
                                new Color(255, 110, 110), new Color(200, 30, 50),
                                Main.rand.NextFloat(0.6f, 1.2f), 16));
                        }
                    }
                }
            }
            _freshTimer = FreshTime;
        }

        public override void OnPlayerUpdate(Player player) {
            if (_stacks <= 0) return;
            if (_freshTimer > 0) {
                _freshTimer--;
                return;
            }
            _decayTimer++;
            if (_decayTimer >= DecayInterval) {
                _decayTimer = 0;
                _stacks--;
            }
        }
    }
}
