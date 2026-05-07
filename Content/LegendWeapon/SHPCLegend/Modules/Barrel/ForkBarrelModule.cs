using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 叉形枪管：追踪光束飞行期间每50帧向两侧各分叉一束子光束
    /// 用 whoAmI→timer 字典实现每束独立计时，IsDerived 标记防递归
    /// </summary>
    internal sealed class ForkBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //电磁分叉蓝绿
        public override Color TintColor => new(0, 220, 180);

        private readonly Dictionary<int, int> _timers = new();

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.15f;
            ctx.ManaCostMul += 0.25f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived) return;
            if (beam.Projectile.owner != Main.myPlayer) return;
            int id = beam.Projectile.whoAmI;
            if (!_timers.TryGetValue(id, out int t)) t = 0;
            t++;
            if (t >= 50) {
                t = 0;
                SpawnFork(beam, -0.44f);
                SpawnFork(beam, 0.44f);
            }
            _timers[id] = t;
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            _timers.Remove(beam.Projectile.whoAmI);
        }

        private static void SpawnFork(CyberTraceBeamProj source, float angleOffset) {
            int dmg = Math.Max((int)(source.Projectile.damage * 0.5f), 1);
            Vector2 vel = source.Projectile.velocity.RotatedBy(angleOffset);
            int idx = Projectile.NewProjectile(
                source.Projectile.GetSource_FromThis(),
                source.Projectile.Center, vel,
                ModContent.ProjectileType<CyberTraceBeamProj>(),
                dmg, 0f, source.Projectile.owner, ai0: Main.rand.Next(3));
            if (idx >= 0 && idx < Main.maxProjectiles
                && Main.projectile[idx].ModProjectile is CyberTraceBeamProj fork) {
                fork.IsDerived = true;
                fork.LifeMul = 0.45f;
                fork.SpeedMul = source.SpeedMul;
            }
        }
    }
}
