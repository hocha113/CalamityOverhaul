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
    /// 月露枪管：光束凝结露珠棱镜，后续光束触碰后被折射成短程派生束。
    /// </summary>
    internal sealed class MoondewBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(185, 220, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -0.12f;
            ctx.BeamLifeMul += 0.10f;
            ctx.CritAdd += 5;
            ctx.ManaCostMul += 0.18f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            int interval = !Main.dayTime && Main.moonPhase <= 2 ? 24 : 38;
            if ((Main.GameUpdateCount + (uint)beam.Projectile.whoAmI) % (uint)interval != 0) return;
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                beam.Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<SHPCMoondewPrismProj>(),
                System.Math.Max(beam.Projectile.damage / 2, 1), 0f, beam.Projectile.owner);
        }
    }

    internal sealed class SHPCMoondewPrismProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 210;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.rotation += 0.03f;
            if (Projectile.owner == Main.myPlayer && Projectile.localAI[0] < MaxRefractions()) {
                TryRefractBeam();
            }
            if (Main.netMode == NetmodeID.Server || Main.GameUpdateCount % 6 != 0) return;
            PRTLoader.AddParticle(new PRT_CyberSquare(
                Projectile.Center + Main.rand.NextVector2Circular(22f, 22f),
                Main.rand.NextVector2Circular(0.5f, 0.5f),
                new Color(190, 235, 255), new Color(90, 130, 210),
                Main.rand.NextFloat(0.4f, 0.9f), Main.rand.Next(16, 30)));
        }

        private int MaxRefractions() {
            bool moonFavored = !Main.dayTime && Main.moonPhase <= 2;
            return moonFavored ? 3 : 1;
        }

        private void TryRefractBeam() {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (!other.active || other.owner != Projectile.owner) continue;
                if (other.type != ModContent.ProjectileType<CyberTraceBeamProj>()) continue;
                if (Vector2.DistanceSquared(other.Center, Projectile.Center) > 42f * 42f) continue;
                Vector2 dir = other.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f));
                int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 13f,
                    ModContent.ProjectileType<CyberTraceBeamProj>(),
                    Math.Max(Projectile.damage, 1), 0f, Projectile.owner, ai0: Main.rand.Next(3), ai1: 1.8f);
                if (idx >= 0 && idx < Main.maxProjectiles
                    && Main.projectile[idx].ModProjectile is CyberTraceBeamProj beam) {
                    beam.IsDerived = true;
                    beam.LifeMul = 0.32f;
                }
                Projectile.localAI[0]++;
                Projectile.timeLeft -= 30;
                return;
            }
        }
    }
}
