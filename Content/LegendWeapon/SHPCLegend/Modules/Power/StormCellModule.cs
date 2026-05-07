using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>
    /// 风暴细胞：蓄力期间每25帧自动向最近敌人释放一束追踪电击光束
    /// 蓄力本身也在持续输出，与主炮形成时间差覆盖
    /// </summary>
    internal sealed class StormCellModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //风暴电蓝
        public override Color TintColor => new(120, 200, 255);

        private int _timer;

        public override void Apply(ref ShootContext ctx) {
            ctx.ChargeTimeMul += -0.1f;
            ctx.OrbExplosionRadiusMul += 0.2f;
            ctx.ManaCostMul += 0.4f;
        }

        public override void OnOrbCharging(CyberChargeOrbProj orb, Player owner) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            _timer++;
            if (_timer < 25) return;
            _timer = 0;
            NPC target = orb.Projectile.Center.FindClosestNPC(480f, false, true);
            if (target == null) return;
            int dmg = Math.Max(orb.Projectile.damage / 5, 1);
            Vector2 dir = (target.Center - orb.Projectile.Center).SafeNormalize(Vector2.UnitX);
            int idx = Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                orb.Projectile.Center, dir * 16f,
                ModContent.ProjectileType<CyberTraceBeamProj>(),
                dmg, 0f, orb.Projectile.owner, ai0: 0f);
            if (idx >= 0 && idx < Main.maxProjectiles
                && Main.projectile[idx].ModProjectile is CyberTraceBeamProj bolt) {
                bolt.IsDerived = true;
                bolt.LifeMul = 0.6f;
                Main.projectile[idx].ai[1] = 3.0f;
            }
        }
    }
}
