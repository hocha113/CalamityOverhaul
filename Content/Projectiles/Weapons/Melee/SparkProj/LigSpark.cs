using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.SparkProj
{
    internal class LigSpark : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = 6;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.penetrate = 2;
            Projectile.MaxUpdates = 3;
            Projectile.timeLeft = 160 * Projectile.MaxUpdates;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15 * Projectile.MaxUpdates;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void AI() {
            int sparky = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.UnusedWhiteBluePurple, 0f, 0f, 100, default, 1f);
            Main.dust[sparky].scale += Main.rand.Next(50) * 0.01f;
            Main.dust[sparky].noGravity = true;
            if (Main.rand.NextBool()) {
                int sparkier = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.UnusedWhiteBluePurple, 0f, 0f, 100, default, 1f);
                Main.dust[sparkier].scale += 0.3f + (Main.rand.Next(50) * 0.01f);
                Main.dust[sparkier].noGravity = true;
                Main.dust[sparkier].velocity *= 0.1f;
            }
            if (Main.rand.NextBool(13)) {
                Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height, DustID.Electric, Projectile.velocity.X, Projectile.velocity.Y);
            }
            Projectile.velocity.Y += 0.01f;
            Projectile.velocity.X *= 0.99f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), target.Center, Main.rand.NextVector2Unit() * Main.rand.Next(3, 5)
                    , ModContent.ProjectileType<SparkLightning>(), Projectile.damage / 3, Projectile.knockBack, Projectile.owner);
            proj.timeLeft = 10;
            proj.penetrate = 3;
            proj.tileCollide = false;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap, Projectile.position);
            for (int i = 0; i < 13; i++) {
                int dust = Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height, DustID.Electric);
                Main.dust[dust].velocity = Main.rand.NextVector2Unit() * Main.rand.Next(3, 8);
                Main.dust[dust].scale = Main.rand.NextFloat(0.2f, 0.5f);
            }
            Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center, Main.rand.NextVector2Unit() * Main.rand.Next(3, 5)
                    , ModContent.ProjectileType<SparkLightning>(), Projectile.damage / 3, Projectile.knockBack, Projectile.owner);
            proj.timeLeft = 10;
            proj.penetrate = 3;
            proj.tileCollide = false;
            return true;
        }
    }
}
