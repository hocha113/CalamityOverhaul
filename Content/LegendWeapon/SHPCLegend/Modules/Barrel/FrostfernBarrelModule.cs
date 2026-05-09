using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 霜蕨枪管：命中后沿弹道背面抽出冰晶脉络，穿线目标共享寒霜。
    /// </summary>
    internal sealed class FrostfernBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(150, 240, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.08f;
            ctx.SpreadMul += -0.18f;
            ctx.BeamSpeedMul += 0.10f;
            ctx.CritAdd += 4;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            Vector2 dir = beam.Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                target.Center, dir,
                ModContent.ProjectileType<SHPCFrostfernLineProj>(),
                System.Math.Max(damageDone / 2, 1), 0f, beam.Projectile.owner);
        }
    }

    internal sealed class SHPCFrostfernLineProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 18;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 end = Projectile.Center + dir * 460f;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, end, 18f, ref point);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn, 240);
        }

        public override void AI() {
            if (Main.netMode == NetmodeID.Server || Main.GameUpdateCount % 3 != 0) return;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 4; i++) {
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    Projectile.Center + dir * Main.rand.NextFloat(20f, 440f) + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Circular(0.8f, 0.8f),
                    new Color(170, 245, 255), new Color(70, 150, 210),
                    Main.rand.NextFloat(0.4f, 1f), Main.rand.Next(12, 24)));
            }
        }
    }
}
