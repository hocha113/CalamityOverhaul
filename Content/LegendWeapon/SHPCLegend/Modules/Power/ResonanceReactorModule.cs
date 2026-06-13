using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>共振反应堆：蓄力周期共振环，引爆时脚下额外冲击</summary>
    internal sealed class ResonanceReactorModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //共振翠绿青
        public override Color TintColor => new(80, 240, 200);

        private const int RingInterval = 45;
        private int _ringTimer;

        public override void Apply(ref ShootContext ctx) {
            ctx.ChargeTimeMul += 0.18f;
            ctx.OrbExplosionRadiusMul += 0.2f;
            ctx.ManaCostMul += 0.24f;
        }

        public override void OnOrbCharging(CyberChargeOrbProj orb, Player owner) {
            _ringTimer++;
            if (_ringTimer < RingInterval) return;
            _ringTimer = 0;
            if (Main.netMode == NetmodeID.Server) return;

            //径向粒子环
            int count = 24;
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 vel = angle.ToRotationVector2() * 6f;
                PRTLoader.NewParticle<PRT_CyberSquare>(orb.Projectile.Center, vel, new Color(140, 255, 220), Main.rand.NextFloat(0.8f, 1.6f)).Configure(new Color(40, 200, 170), Main.rand.Next(18, 30));
            }
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            //引爆点已有主爆，玩家脚下追加一个共振冲击：覆盖近战补足
            if (orb.Projectile.owner != Main.myPlayer) return;
            Player owner = Main.player[orb.Projectile.owner];
            if (owner == null || !owner.active) return;
            int dmg = Math.Max(orb.Projectile.damage / 2, 1);
            int idx = Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                owner.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, 0f, orb.Projectile.owner, ai0: 0.3f);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].localAI[2] = 200f;
            }
        }
    }
}
