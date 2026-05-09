using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 珊瑚枪管：命中点长出珊瑚锚，锚点间连成伤害礁线，右键爆炸触发同步浪涌。
    /// </summary>
    internal sealed class CoralReefBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(255, 115, 150);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.06f;
            ctx.BeamLifeMul += 0.12f;
            ctx.OrbExplosionRadiusMul += 0.12f;
            ctx.ManaCostMul += 0.18f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                target.Center, Vector2.Zero,
                ModContent.ProjectileType<SHPCCoralAnchorProj>(),
                System.Math.Max(damageDone / 3, 1), 0f, beam.Projectile.owner);
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != orb.Projectile.owner) continue;
                if (proj.type != ModContent.ProjectileType<SHPCCoralAnchorProj>()) continue;
                if (Vector2.DistanceSquared(proj.Center, orb.Projectile.Center) > 900f * 900f) continue;
                int idx = Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                    proj.Center, Vector2.Zero,
                    ModContent.ProjectileType<CyberDetonationProj>(),
                    System.Math.Max(orb.Projectile.damage / 3, 1), 0f, orb.Projectile.owner, ai0: 0.45f);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].localAI[2] = 130f;
                }
            }
        }
    }

    internal sealed class SHPCCoralAnchorProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 420;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            bool hit = false;
            float point = 0f;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (!other.active || other.owner != Projectile.owner || other.whoAmI == Projectile.whoAmI) continue;
                if (other.type != Projectile.type) continue;
                if (Vector2.DistanceSquared(other.Center, Projectile.Center) > 360f * 360f) continue;
                hit |= Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, other.Center, 12f, ref point);
            }
            return hit;
        }

        public override void AI() {
            if (Main.netMode == NetmodeID.Server || Main.GameUpdateCount % 8 != 0) return;
            PRTLoader.AddParticle(new PRT_CyberSquare(
                Projectile.Center + Main.rand.NextVector2Circular(18f, 18f),
                Main.rand.NextVector2Circular(0.6f, 0.6f),
                new Color(255, 110, 140), new Color(80, 220, 190),
                Main.rand.NextFloat(0.45f, 1f), Main.rand.Next(14, 28)));
        }
    }
}
