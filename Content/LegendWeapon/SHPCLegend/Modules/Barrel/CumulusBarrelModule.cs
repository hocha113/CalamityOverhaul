using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 积云枪管：光束留下可被后续 SHPC 弹幕充能的云核，满能后降下雷雨。
    /// </summary>
    internal sealed class CumulusBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(165, 215, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += 0.06f;
            ctx.DamageMul += -0.12f;
            ctx.BeamLifeMul += 0.14f;
            ctx.ManaCostMul += 0.15f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            if ((Main.GameUpdateCount + (uint)beam.Projectile.whoAmI) % 42 != 0) return;
            int damage = System.Math.Max(beam.Projectile.damage / 3, 1);
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                beam.Projectile.Center + Main.rand.NextVector2Circular(30f, 18f),
                Main.rand.NextVector2Circular(0.8f, 0.5f),
                ModContent.ProjectileType<SHPCCumulusNodeProj>(),
                damage, 0f, beam.Projectile.owner);
            if (Main.netMode != NetmodeID.Server) {
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.25f, Pitch = 0.4f }, beam.Projectile.Center);
            }
        }
    }

    internal sealed class SHPCCumulusNodeProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = 72;
            Projectile.height = 44;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.velocity *= 0.94f;
            Projectile.localAI[0] = MathF.Min(Projectile.localAI[0] + PassiveCharge(), 100f);
            if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 5 == 0) {
                Color color = Projectile.localAI[0] > 70f ? new Color(210, 240, 255) : new Color(150, 190, 220);
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    Projectile.Center + Main.rand.NextVector2Circular(44f, 20f),
                    Main.rand.NextVector2Circular(0.5f, 0.5f),
                    color, new Color(90, 130, 170),
                    Main.rand.NextFloat(0.5f, 1.1f), Main.rand.Next(18, 34)));
            }
            if (Projectile.localAI[0] < 100f) return;
            if (Projectile.owner == Main.myPlayer) {
                ReleaseRain();
            }
            Projectile.Kill();
        }

        private float PassiveCharge() {
            float charge = 0.08f;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (!other.active || other.owner != Projectile.owner || other.whoAmI == Projectile.whoAmI) continue;
                if (other.type != ModContent.ProjectileType<CyberTraceBeamProj>()
                    && other.type != ModContent.ProjectileType<CyberChargeOrbProj>()) continue;
                if (Vector2.DistanceSquared(other.Center, Projectile.Center) > 130f * 130f) continue;
                charge += 3.2f;
            }
            return charge;
        }

        private void ReleaseRain() {
            for (int i = -1; i <= 1; i++) {
                Vector2 spawn = Projectile.Center + new Vector2(i * 36f, -10f);
                int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawn, Vector2.UnitY * 12f,
                    ModContent.ProjectileType<CyberTraceBeamProj>(),
                    Math.Max(Projectile.damage, 1), 0f, Projectile.owner, ai0: Main.rand.Next(3), ai1: 0.2f);
                if (idx >= 0 && idx < Main.maxProjectiles
                    && Main.projectile[idx].ModProjectile is CyberTraceBeamProj beam) {
                    beam.IsDerived = true;
                    beam.LifeMul = 0.35f;
                    beam.SpeedMul = 1.25f;
                }
            }
        }
    }
}
