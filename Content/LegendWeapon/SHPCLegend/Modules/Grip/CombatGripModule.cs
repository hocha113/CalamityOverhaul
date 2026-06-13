using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>战斗握把：击杀叠战意层，动态注入攻速/伤害，PostUpdate 衰减</summary>
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
            ctx.SpreadMul += -0.08f;
            ctx.ManaCostMul += 0.2f;
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
                            PRTLoader.NewParticle<PRT_CyberSquare>(p.Center, vel, new Color(255, 110, 110), Main.rand.NextFloat(0.6f, 1.2f)).Configure(new Color(200, 30, 50), 16);
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
