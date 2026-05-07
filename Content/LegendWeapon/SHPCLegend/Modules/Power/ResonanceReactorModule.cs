using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>
    /// 共振反应堆：蓄力期间每秒生成一次共振环（视觉粒子+轻量震荡），引爆时玩家脚下额外触发一次共振冲击
    /// 视觉脉冲只在弹幕拥有者侧本机推进，伤害弹幕由本机生成
    /// </summary>
    internal sealed class ResonanceReactorModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //共振翠绿青
        public override Color TintColor => new(80, 240, 200);

        private const int RingInterval = 45;
        private int _ringTimer;

        public override void Apply(ref ShootContext ctx) {
            ctx.ChargeTimeMul += 0.15f;
            ctx.OrbExplosionRadiusMul += 0.25f;
            ctx.ManaCostMul += 0.20f;
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
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    orb.Projectile.Center, vel,
                    new Color(140, 255, 220), new Color(40, 200, 170),
                    Main.rand.NextFloat(0.8f, 1.6f), Main.rand.Next(18, 30)));
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
