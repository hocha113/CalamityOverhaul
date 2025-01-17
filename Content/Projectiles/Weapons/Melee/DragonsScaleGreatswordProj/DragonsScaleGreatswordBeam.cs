using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DragonsScaleGreatswordProj
{
    internal class DragonsScaleGreatswordBeam : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "SporeCloud";
        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.width = Projectile.height = 24;
            Projectile.penetrate = 1;
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.MaxUpdates = 3;
            Projectile.timeLeft = 120 * Projectile.MaxUpdates;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            for (int i = 0; i < 6; i++) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Main.rand.NextFloat(220 * CWRUtils.atoR, 320 * CWRUtils.atoR).ToRotationVector2() * Main.rand.Next(5, 11)
                    , ModContent.ProjectileType<SporeCloud>(), Projectile.damage / 3, Projectile.knockBack, Projectile.owner);
            }
            Projectile.Explode(32);
            return true;
        }

        public override void AI() {
            Projectile.scale += 0.01f;
            for (int i = 0; i < 3; i++) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.JungleSpore, Projectile.velocity.X, Projectile.velocity.Y);
                Main.dust[dust].noGravity = true;
                CWRDust.SpanCycleDust(Projectile, DustID.JungleTorch, DustID.JungleTorch);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < Main.rand.Next(3, 6); i++) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Main.rand.NextVector2Unit() * Main.rand.Next(6, 9)
                    , ModContent.ProjectileType<SporeCloud>(), Projectile.damage / 3, Projectile.knockBack, Projectile.owner);
            }
            Projectile.Explode(42);
            target.AddBuff(BuffID.Poisoned, 1200);
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
