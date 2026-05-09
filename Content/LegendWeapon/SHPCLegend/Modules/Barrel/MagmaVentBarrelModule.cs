using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 岩浆枪管：命中与消亡处留下熔岩喷口，周期喷发形成区域封锁。
    /// </summary>
    internal sealed class MagmaVentBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(255, 105, 30);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += 0.10f;
            ctx.HomingMul += -0.25f;
            ctx.BeamSpeedMul += -0.08f;
            ctx.ManaCostMul += 0.22f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            SpawnVent(beam, target.Bottom, System.Math.Max(damageDone / 2, 1));
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            if (beam.IsDerived) return;
            SpawnVent(beam, beam.Projectile.Center, System.Math.Max(beam.Projectile.damage / 3, 1));
        }

        private static void SpawnVent(CyberTraceBeamProj beam, Vector2 center, int damage) {
            if (beam.Projectile.owner != Main.myPlayer) return;
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                center, Vector2.Zero,
                ModContent.ProjectileType<SHPCMagmaVentProj>(),
                damage, 0f, beam.Projectile.owner);
        }
    }

    internal sealed class SHPCMagmaVentProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int Lifetime = 150;
        private const int PulseInterval = 30;

        public override void SetDefaults() {
            Projectile.width = 56;
            Projectile.height = 180;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] > 0f) {
                Projectile.localAI[0]--;
            }
            int age = Lifetime - Projectile.timeLeft;
            Tile tile = Framing.GetTileSafely(Projectile.Center.ToTileCoordinates());
            bool nearLava = Projectile.Center.Y / 16f > Main.UnderworldLayer
                || (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Lava);
            int interval = nearLava ? 20 : PulseInterval;
            if (age % interval == 0) {
                Projectile.localAI[0] = 9f;
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 18; i++) {
                        Vector2 vel = new(Main.rand.NextFloat(-2.4f, 2.4f), Main.rand.NextFloat(-8f, -3f));
                        PRTLoader.AddParticle(new PRT_CyberSquare(
                            Projectile.Center + new Vector2(Main.rand.NextFloat(-28f, 28f), 0f), vel,
                            new Color(255, 115, 25), new Color(130, 30, 10),
                            Main.rand.NextFloat(0.8f, 1.8f), Main.rand.Next(20, 42)));
                    }
                }
            }
            Lighting.AddLight(Projectile.Center, new Vector3(1.1f, 0.35f, 0.08f) * (Projectile.localAI[0] / 9f));
        }

        public override bool? CanDamage() => Projectile.localAI[0] > 0f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float width = 28f + Projectile.localAI[0] * 2.5f;
            float point = 0f;
            Vector2 top = Projectile.Center - Vector2.UnitY * 190f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, top, width, ref point);
        }
    }
}
