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
        private const float MaxDistanceBonus = 0.60f;

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
            float bonus = MathHelper.Clamp((dist - LongRangeThreshold) / 600f, 0f, 1f) * MaxDistanceBonus;
            int extra = Math.Max((int)(damageDone * (0.20f + bonus)), 1);
            target.SimpleStrikeNPC(extra, hit.HitDirection, false, 0f, hit.DamageType, false, 0f, true);
            SpawnImpactParticles(target.Center);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if (laser.Projectile.owner != Main.myPlayer) return;
            //激光 25% 节流
            if (Main.rand.NextFloat() > 0.25f) return;
            Player owner = Main.player[laser.Projectile.owner];
            if (owner == null || !owner.active) return;
            float dist = Vector2.Distance(owner.Center, target.Center);
            if (dist < LongRangeThreshold) return;
            float bonus = MathHelper.Clamp((dist - LongRangeThreshold) / 600f, 0f, 1f) * 0.30f;
            int extra = Math.Max((int)(damageDone * (0.15f + bonus)), 1);
            target.SimpleStrikeNPC(extra, hit.HitDirection, false, 0f, hit.DamageType, false, 0f, true);
            SpawnImpactParticles(target.Center);
        }

        private static void SpawnImpactParticles(Vector2 center) {
            if (Main.netMode == Terraria.ID.NetmodeID.Server) return;
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                PRTLoader.NewParticle<PRT_CyberSquare>(center, vel, new Color(220, 240, 255), Main.rand.NextFloat(1.0f, 2.2f)).Configure(new Color(120, 200, 255), Main.rand.Next(20, 35));
            }
        }
    }
}
