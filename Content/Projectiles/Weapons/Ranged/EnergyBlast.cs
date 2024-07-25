using CalamityMod.Buffs.DamageOverTime;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class EnergyBlast : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.timeLeft = 164;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 7;
        }

        public override void AI() {
            if (Projectile.Center.To(Main.player[Projectile.owner].Center).LengthSquared() <= 1500 * 1500) {
                for (int i = 0; i < 3; i++) {
                    int sparkLifetime = Main.rand.Next(22, 36);
                    float sparkScale = Main.rand.NextFloat(1f, 1.3f);
                    Color sparkColor = Main.DiscoColor;
                    Vector2 sparkVelocity = Projectile.velocity * (0.5f - (i * 0.1f));
                    BaseParticle spark = new PRK_Spark(Projectile.Center + (Projectile.velocity * i), sparkVelocity, false, sparkLifetime, sparkScale, sparkColor);
                    DRKLoader.AddParticle(spark);
                }
            }
        }

        public static void SpanDust(Projectile projectile) {
            int[] dustTypes = new int[] { 15, 74, 73, 162, 90, 173, 57 };
            int height = 40;
            Vector2 dustRotation = (projectile.rotation - 1.57079637f).ToRotationVector2();
            Vector2 dustVel = dustRotation * projectile.velocity.Length() * projectile.MaxUpdates;
            _ = SoundEngine.PlaySound(SoundID.Item14, projectile.position);
            projectile.position = projectile.Center;
            projectile.width = projectile.height = height;
            projectile.Center = projectile.position;
            projectile.maxPenetrate = -1;
            projectile.penetrate = -1;
            projectile.usesLocalNPCImmunity = true;
            projectile.localNPCHitCooldown = 10;
            projectile.damage /= 2;
            projectile.Damage();
            int inc;
            for (int i = 0; i < 20; i = inc + 1) {
                int dustID = Dust.NewDust(new Vector2(projectile.position.X, projectile.position.Y), projectile.width, projectile.height, dustTypes[Main.rand.Next(dustTypes.Length)], 0f, 0f, 200, default, 2.1f);
                Main.dust[dustID].position = projectile.Center + (Vector2.UnitY.RotatedByRandom(3.1415927410125732) * (float)Main.rand.NextDouble() * projectile.width / 2f);
                Main.dust[dustID].noGravity = true;
                Dust dust = Main.dust[dustID];
                dust.velocity *= 3f;
                dust = Main.dust[dustID];
                dust.velocity += dustVel * Main.rand.NextFloat();
                dustID = Dust.NewDust(new Vector2(projectile.position.X, projectile.position.Y), projectile.width, projectile.height, dustTypes[Main.rand.Next(dustTypes.Length)], 0f, 0f, 100, default, 1.1f);
                Main.dust[dustID].position = projectile.Center + (Vector2.UnitY.RotatedByRandom(3.1415927410125732) * (float)Main.rand.NextDouble() * projectile.width / 2f);
                dust = Main.dust[dustID];
                dust.velocity *= 2f;
                Main.dust[dustID].noGravity = true;
                Main.dust[dustID].fadeIn = 1f;
                dust = Main.dust[dustID];
                dust.velocity += dustVel * Main.rand.NextFloat();
                inc = i;
            }
            for (int j = 0; j < 10; j = inc + 1) {
                int dustID = Dust.NewDust(new Vector2(projectile.position.X, projectile.position.Y), projectile.width, projectile.height, dustTypes[Main.rand.Next(dustTypes.Length)], 0f, 0f, 0, default, 2.5f);
                Main.dust[dustID].position = projectile.Center + (Vector2.UnitX.RotatedByRandom(3.1415927410125732).RotatedBy((double)projectile.velocity.ToRotation(), default) * projectile.width / 3f);
                Main.dust[dustID].noGravity = true;
                Dust dust = Main.dust[dustID];
                dust.velocity *= 0.5f;
                dust = Main.dust[dustID];
                dust.velocity += dustVel * (0.6f + (0.6f * Main.rand.NextFloat()));
                inc = j;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<ElementalMix>(), 60);
            if (Projectile.numHits == 0)
                SpanDust(Projectile);
        }
    }
}
