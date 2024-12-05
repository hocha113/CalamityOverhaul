using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.SparkProj
{
    internal class SparkBall : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public int damageScale;
        public override void SetDefaults() {
            Projectile.width = 6;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 60;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            Projectile.DamageType = DamageClass.Melee;
            damageScale = Main.rand.Next(Projectile.localNPCHitCooldown * 3);
        }

        public override bool? CanHitNPC(NPC target) => damageScale > 0 ? false : base.CanHitNPC(target);
        public override bool CanHitPvp(Player target) => damageScale > 0 ? false : base.CanHitPvp(target);
        public override bool OnTileCollide(Vector2 oldVelocity) => false;

        public override void AI() {
            damageScale--;
            if (float.IsNaN(Projectile.velocity.X)) {
                Projectile.velocity.X *= -0.1f;
            }

            if (float.IsNaN(Projectile.velocity.Y)) {
                if (Projectile.velocity.Y > 1f) {
                    Projectile.velocity.Y *= -0.5f;
                }
            }

            Projectile.ai[0] = Math.Min(Projectile.ai[0] + 1f, 5f);
            if (Projectile.ai[0] == 5f) {
                if (Projectile.velocity.Y == 0f && Projectile.velocity.X != 0f) {
                    Projectile.velocity.X *= 0.97f;

                    if (Math.Abs(Projectile.velocity.X) < 0.01f) {
                        Projectile.velocity.X = 0f;
                        Projectile.netUpdate = true;
                    }
                }
                Projectile.velocity.Y += 0.2f;
            }

            Projectile.rotation += Projectile.velocity.X * 0.1f;

            GenerateDust();

            if (Projectile.velocity.Y is > 0.15f and < 0.25f) {
                Projectile.velocity.X *= 0.8f;
            }

            Projectile.rotation = -Projectile.velocity.X * 0.05f;
            if (Projectile.velocity.Y > 16f) {
                Projectile.velocity.Y = 16f;
            }
        }

        private void GenerateDust() {
            int sparky = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.UnusedWhiteBluePurple, 0f, 0f, 100, default, 1f);
            Main.dust[sparky].scale += Main.rand.Next(50) * 0.01f;
            Main.dust[sparky].noGravity = true;

            if (Main.rand.NextBool()) {
                int sparkier = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.UnusedWhiteBluePurple, 0f, 0f, 100, default, 1f);
                Main.dust[sparkier].scale += 0.3f + Main.rand.Next(50) * 0.01f;
                Main.dust[sparkier].noGravity = true;
                Main.dust[sparkier].velocity *= 0.1f;
            }
        }
    }
}
