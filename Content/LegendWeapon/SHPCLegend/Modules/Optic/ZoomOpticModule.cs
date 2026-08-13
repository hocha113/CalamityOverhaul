using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>焦距瞄具，≥600px 命中追加白热打击</summary>
    internal sealed class ZoomOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //远焦冷蓝
        public override Color TintColor => new(180, 220, 255);

        private const float LongRangeThreshold = 600f;
        private const float LegacyPeakDistance = 1800f;
        private const float BeamGrowthAtLegacyPeak = 0.75f;
        private const float LaserGrowthAtLegacyPeak = 0.54f;

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamSpeedMul += 0.6f;
            ctx.BeamLifeMul += 0.4f;
            ctx.AttackSpeedMul += -0.18f;
            ctx.HomingMul += -0.24f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.Projectile.owner != Main.myPlayer) return;
            Player owner = Main.player[beam.Projectile.owner];
            if (owner == null || !owner.active) return;
            float dist = Vector2.Distance(owner.Center, target.Center);
            if (dist < LongRangeThreshold) return;
            float distanceGrowth = GetDistanceGrowth(dist);
            int extra = Math.Max((int)(damageDone * (0.35f + distanceGrowth * BeamGrowthAtLegacyPeak)), 1);
            target.SimpleStrikeNPC(extra, hit.HitDirection, false, 0f, hit.DamageType, false, 0f, true);
            SpawnImpactParticles(target.Center, beam.FlightDirection, distanceGrowth);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if (laser.Projectile.owner != Main.myPlayer) return;
            //激光 25% 节流
            if (Main.rand.NextFloat() > 0.25f) return;
            Player owner = Main.player[laser.Projectile.owner];
            if (owner == null || !owner.active) return;
            float dist = Vector2.Distance(owner.Center, target.Center);
            if (dist < LongRangeThreshold) return;
            float distanceGrowth = GetDistanceGrowth(dist);
            int extra = Math.Max((int)(damageDone * (0.20f + distanceGrowth * LaserGrowthAtLegacyPeak)), 1);
            target.SimpleStrikeNPC(extra, hit.HitDirection, false, 0f, hit.DamageType, false, 0f, true);
            SpawnImpactParticles(target.Center, laser.Projectile.rotation.ToRotationVector2(), distanceGrowth);
        }

        /// <summary>距离收益单调递增；1800px 达到旧峰值，之后按对数边际递减</summary>
        private static float GetDistanceGrowth(float distance) {
            float normalizedDistance = (distance - LongRangeThreshold)
                / (LegacyPeakDistance - LongRangeThreshold);
            return MathF.Log(1f + normalizedDistance) / MathF.Log(2f);
        }

        /// <summary>远焦贯穿锥，沿弹道过靶延伸，距离越远越长越亮</summary>
        private static void SpawnImpactParticles(Vector2 center, Vector2 dir, float growth) {
            if (Main.netMode == Terraria.ID.NetmodeID.Server) return;
            dir = dir.SafeNormalize(Vector2.UnitX);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

            //白热窄锥火花，速度拉伸穿靶向前
            int sparks = 5 + (int)(growth * 3f);
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f)) * Main.rand.NextFloat(4.5f, 9f + growth * 4f);
                PRTLoader.NewParticle<PRT_Spark>(center + perp * Main.rand.NextFloat(-6f, 6f), vel,
                    new Color(235, 248, 255), Main.rand.NextFloat(0.5f, 0.95f)).Configure(false, Main.rand.Next(10, 16));
            }

            //少量方屑横散衬托锥向
            for (int i = 0; i < 3; i++) {
                Vector2 vel = perp * Main.rand.NextFloat(-2.5f, 2.5f) + dir * Main.rand.NextFloat(0.5f, 2f);
                PRTLoader.NewParticle<PRT_CyberSquare>(center, vel,
                    new Color(200, 235, 255), Main.rand.NextFloat(0.7f, 1.2f)).Configure(new Color(120, 200, 255), Main.rand.Next(12, 20));
            }

            //薄锐小环钉住弹着点
            PRTLoader.NewParticle<PRT_StarPulseRing>(center, Vector2.Zero,
                new Color(190, 225, 255), 0.03f).Configure(0.03f, 0.18f + growth * 0.08f, 12);
        }
    }
}
