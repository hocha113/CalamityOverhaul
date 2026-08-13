using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>谐振机匣，多束近距互生共振电弧，视觉+微伤</summary>
    internal sealed class HarmonicFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //谐振翡翠绿
        public override Color TintColor => new(120, 240, 180);

        private const int ResonateInterval = 18;
        private const float ResonateRange = 320f;
        private const float ArcDamageRatio = 0.20f;
        /// <summary>取弧音效最小间隔（帧）</summary>
        private const int ZapSoundGap = 10;

        //每束独立计时，防争抢触发
        private readonly Dictionary<int, int> _timers = new();
        /// <summary>上次取弧音帧号，多束同发限频</summary>
        private uint _lastZapTick;

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamCountAdd += 1;
            ctx.DamageMul += -0.06f;
            ctx.SpreadMul += 0.18f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived) return;
            if (beam.Projectile.owner != Main.myPlayer || beam.Projectile.numUpdates != -1) return;
            int id = beam.Projectile.whoAmI;
            if (!_timers.TryGetValue(id, out int t)) t = 0;
            t++;
            if (t < ResonateInterval) {
                _timers[id] = t;
                return;
            }
            _timers[id] = 0;

            //更高 whoAmI 同型束最近一对，id 配对防重复
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
            int dmg = System.Math.Max((int)(beam.Projectile.damage * ArcDamageRatio), 1);
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
            //取弧轻 zap，本路径仅所有者客户端
            if (Main.GameUpdateCount - _lastZapTick >= ZapSoundGap) {
                _lastZapTick = Main.GameUpdateCount;
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with {
                    Volume = 0.32f,
                    Pitch = Main.rand.NextFloat(0.1f, 0.35f)
                }, beam.Projectile.Center + delta * 0.5f);
            }
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            _timers.Remove(beam.Projectile.whoAmI);
        }
    }
}
