using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 蜂巢枪管：左键铺设信息素，右键引爆时派出赛博蜂群分头俯冲标记目标。
    /// </summary>
    internal sealed class HiveBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(255, 205, 70);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += 0.08f;
            ctx.DamageMul += -0.14f;
            ctx.HomingMul += 0.12f;
            ctx.ManaCostMul += 0.20f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                eff.ApplyPheromone(360, beam.Projectile.owner);
            }
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            List<NPC> targets = SHPCNPCEffects.CollectPheromoneTargets(orb.Projectile.owner, orb.Projectile.Center, 900f, 6);
            if (targets.Count == 0) return;
            int droneCount = targets.Count * 2;
            for (int i = 0; i < droneCount; i++) {
                NPC target = targets[i % targets.Count];
                Vector2 spawn = orb.Projectile.Center + Main.rand.NextVector2Circular(70f, 70f);
                Vector2 vel = (target.Center - spawn).SafeNormalize(Main.rand.NextVector2CircularEdge(1f, 1f)) * 9f;
                Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                    spawn, vel,
                    ModContent.ProjectileType<SHPCHiveDroneProj>(),
                    System.Math.Max(orb.Projectile.damage / 5, 1), 0f, orb.Projectile.owner, ai0: target.whoAmI);
            }
        }
    }

    internal sealed class SHPCHiveDroneProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 110;
            Projectile.extraUpdates = 1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            NPC target = null;
            int targetIndex = (int)Projectile.ai[0];
            if (targetIndex >= 0 && targetIndex < Main.maxNPCs && Main.npc[targetIndex].active) {
                target = Main.npc[targetIndex];
            }
            target ??= Projectile.Center.FindClosestNPC(460f, false, true);
            if (target != null) {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Projectile.velocity.SafeNormalize(Vector2.UnitX)) * 13f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.08f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Main.netMode == NetmodeID.Server || Main.GameUpdateCount % 3 != 0) return;
            PRTLoader.AddParticle(new PRT_CyberSquare(
                Projectile.Center, -Projectile.velocity.SafeNormalize(Vector2.Zero) * 1.2f,
                new Color(255, 210, 80), new Color(120, 90, 25),
                Main.rand.NextFloat(0.35f, 0.75f), Main.rand.Next(8, 16)));
        }
    }
}
