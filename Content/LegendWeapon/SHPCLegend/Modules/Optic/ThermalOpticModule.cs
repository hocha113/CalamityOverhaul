using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>
    /// 热成像瞄具（热源成像）：红外锁定让光束紧咬目标，并持续为命中目标累积热量。
    /// 热量攒满后目标会发生热斑爆裂，向四周喷出灼热冲击。
    /// </summary>
    internal sealed class ThermalOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //热成像火粉
        public override Color TintColor => new(255, 90, 110);

        private const int HeatThreshold = 6;
        private const int HeatDuration = 240;

        public override void Apply(ref ShootContext ctx) {
            ctx.HomingMul += 1.2f;
            ctx.CritAdd += 4;
            ctx.SpreadMul += -0.2f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            Heat(beam.Projectile, target, Math.Max(beam.Projectile.damage, 1));
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if ((int)Main.GameUpdateCount % 8 != 0) return;
            Heat(laser.Projectile, target, Math.Max(laser.Projectile.damage, 1));
        }

        private void Heat(Projectile src, NPC target, int dmg) {
            if (src.owner != Main.myPlayer) return;
            if (!target.TryGetGlobalNPC(out SHPCNPCEffects eff)) return;
            int stacks = eff.ApplyThermalHeat(HeatDuration, src.owner);
            if (stacks < HeatThreshold) return;
            eff.ResetThermalHeat();
            Burst(src, target, dmg);
        }

        private static void Burst(Projectile src, NPC target, int dmg) {
            int idx = Projectile.NewProjectile(src.GetSource_FromThis(),
                target.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                Math.Max(dmg, 1), 0f, src.owner, ai0: 0.4f);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].localAI[2] = 130f;
                Main.projectile[idx].usesLocalNPCImmunity = true;
                Main.projectile[idx].localNPCHitCooldown = -1;
            }
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.5f, Pitch = 0.1f }, target.Center);
                for (int i = 0; i < 10; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(target.Center, vel, new Color(255, 150, 60), Main.rand.NextFloat(0.6f, 1.3f)).Configure(new Color(255, 60, 30), Main.rand.Next(14, 26));
                }
                PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero, new Color(255, 120, 50, 0), 0.05f).Configure(0.05f, 0.5f, 22);
            }
            SHPCNaturalFx.Shake(3f);
        }
    }
}
