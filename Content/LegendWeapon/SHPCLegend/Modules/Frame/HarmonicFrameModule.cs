using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>
    /// 谐振机匣：当多束 SHPC 光束同时存在时彼此产生共振电弧
    /// 每束光束在 OnBeamAI 中按窗口扫描其它存活光束，若距离足够近则在两束之间生成短促电弧
    /// 电弧仅作为视觉/微伤强化，避免修改光束自身属性带来的耦合
    /// </summary>
    internal sealed class HarmonicFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //谐振翡翠绿
        public override Color TintColor => new(120, 240, 180);

        private const int ResonateInterval = 18;
        private const float ResonateRange = 320f;

        //每束光束独立计时，避免互相争抢共振触发
        private readonly Dictionary<int, int> _timers = new();

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamCountAdd += 1;
            ctx.DamageMul += -0.05f;
            ctx.SpreadMul += 0.15f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived) return;
            if (beam.Projectile.owner != Main.myPlayer) return;
            int id = beam.Projectile.whoAmI;
            if (!_timers.TryGetValue(id, out int t)) t = 0;
            t++;
            if (t < ResonateInterval) {
                _timers[id] = t;
                return;
            }
            _timers[id] = 0;

            //在所有更高 whoAmI 的同型光束中选最近一束生成电弧（id 配对避免重复）
            int beamType = ModContent.ProjectileType<CyberTraceBeamProj>();
            float bestSq = ResonateRange * ResonateRange;
            Projectile bestPair = null;
            for (int i = id + 1; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != beamType) continue;
                if (p.owner != beam.Projectile.owner) continue;
                if (p.ModProjectile is CyberTraceBeamProj other && other.IsDerived) continue;
                float distSq = Vector2.DistanceSquared(p.Center, beam.Projectile.Center);
                if (distSq < bestSq) {
                    bestSq = distSq;
                    bestPair = p;
                }
            }
            if (bestPair == null) return;

            Vector2 delta = bestPair.Center - beam.Projectile.Center;
            int dmg = System.Math.Max(beam.Projectile.damage / 5, 1);
            int idx = Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                beam.Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDataArcProj>(),
                dmg, 0f, beam.Projectile.owner,
                ai0: delta.X, ai1: delta.Y);
            if (idx >= 0 && idx < Main.maxProjectiles
                && Main.projectile[idx].ModProjectile is CyberDataArcProj arc) {
                arc.CoreColor = new Color(220, 255, 220).ToVector3();
                arc.GlowColor = new Color(80, 220, 150).ToVector3();
            }
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            _timers.Remove(beam.Projectile.whoAmI);
        }
    }
}
