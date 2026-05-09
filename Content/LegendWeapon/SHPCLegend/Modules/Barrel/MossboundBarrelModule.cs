using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 苔藓枪管：光束铺设湿苔区域，右键能量球吸收苔痕扩大最终爆炸。
    /// </summary>
    internal sealed class MossboundBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(70, 175, 75);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamSpeedMul += -0.18f;
            ctx.BeamLifeMul += 0.20f;
            ctx.OrbExplosionRadiusMul += 0.10f;
            ctx.ManaCostMul += 0.16f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            if ((Main.GameUpdateCount + (uint)beam.Projectile.whoAmI) % 18 != 0) return;
            int idx = Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                beam.Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<SHPCMossPatchProj>(),
                System.Math.Max(beam.Projectile.damage / 8, 1), 0f, beam.Projectile.owner);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].localAI[0] = 0f;
            }
        }

        public override void OnOrbFlyingAI(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            int absorbed = 0;
            for (int i = 0; i < Main.maxProjectiles && absorbed < 5; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != orb.Projectile.owner) continue;
                if (proj.type != ModContent.ProjectileType<SHPCMossPatchProj>()) continue;
                if (Vector2.DistanceSquared(proj.Center, orb.Projectile.Center) > 180f * 180f) continue;
                proj.Kill();
                absorbed++;
            }
            if (absorbed > 0) {
                orb.ExplosionRadiusMul += 0.06f * absorbed;
            }
        }
    }

    internal sealed class SHPCMossPatchProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = 96;
            Projectile.height = 96;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            float radius = 70f + Projectile.localAI[0] * 20f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                if (Vector2.DistanceSquared(npc.Center, Projectile.Center) > radius * radius) continue;
                if (npc.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                    eff.ApplyMoss(90, 1);
                }
            }
            if (Main.netMode == NetmodeID.Server || Main.GameUpdateCount % 8 != 0) return;
            PRTLoader.AddParticle(new PRT_CyberSquare(
                Projectile.Center + Main.rand.NextVector2Circular(radius, radius * 0.35f),
                new Vector2(0f, Main.rand.NextFloat(-0.5f, 0.2f)),
                new Color(80, 190, 70), new Color(30, 90, 40),
                Main.rand.NextFloat(0.35f, 0.8f), Main.rand.Next(20, 45)));
        }
    }
}
