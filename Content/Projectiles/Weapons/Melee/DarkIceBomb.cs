using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class DarkIceBomb : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public ref float Time => ref Projectile.ai[0];
        public Color InnerColor = Color.AliceBlue;
        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.timeLeft = 600;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 2;
            Projectile.coldDamage = true;
        }

        public override void AI() {
            if (Math.Abs(Projectile.velocity.X) + Math.Abs(Projectile.velocity.Y) < 16f) {
                Projectile.velocity *= 1.045f;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Lighting.AddLight(Projectile.Center, Color.AliceBlue.ToVector3() * 0.2f);
            Player Owner = Main.player[Projectile.owner];
            float targetDist = Vector2.Distance(Owner.Center, Projectile.Center);

            if (Projectile.timeLeft % 2 == 0 && Time > 5f && targetDist < 1400f) {
                PRT_SparkAlpha spark = new(Projectile.Center, Projectile.velocity * 0.05f, false, 8, 2.3f, Color.DarkBlue);
                PRTLoader.AddParticle(spark);
            }

            if (Main.rand.NextBool(3) && Time > 5f && targetDist < 1400f) {
                PRT_Bloomlight orb = new PRT_Bloomlight(Projectile.Center + Main.rand.NextVector2Circular(10, 10)
                    , Projectile.velocity * Main.rand.NextFloat(0.05f, 0.5f), Color.WhiteSmoke, Main.rand.NextFloat(0.2f, 0.45f), Main.rand.Next(6, 9), true, false);
                PRTLoader.AddParticle(orb);
            }

            if (Projectile.timeLeft % 2 == 0 && Time > 5f && targetDist < 1400f) {
                PRT_Line spark2 = new(Projectile.Center, -Projectile.velocity * 0.05f, false, 10, 0.7f, Color.AliceBlue);
                PRTLoader.AddParticle(spark2);
            }

            Time++;
        }

        private void SpanIceEffect(float offset = 0) {
            for (int i = 0; i < 30; i++) {
                int dustSpawns = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.DungeonWater, 0f, 0f, 0, default, Main.rand.NextFloat(1f, 2f));
                Main.dust[dustSpawns].noGravity = true;
                Main.dust[dustSpawns].velocity *= 4f;

                Vector2 randVr = VaultUtils.RandVrInAngleRange(-170, -10, Main.rand.Next(9, 32)).RotatedBy(offset);
                PRT_SparkAlpha spark = new PRT_SparkAlpha(Projectile.Center, randVr, true, 12, Main.rand.NextFloat(1.3f, 2.2f), Color.Blue);
                PRTLoader.AddParticle(spark);
                PRT_SparkAlpha spark2 = new PRT_SparkAlpha(Projectile.Center, randVr, false, 9, Main.rand.NextFloat(1.1f, 1.5f), Color.AntiqueWhite);
                PRTLoader.AddParticle(spark2);
            }
            for (int j = 0; j < 20; ++j) {
                int dustSpawns = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.BlueCrystalShard, 0f, 0f, 0, new Color(), 1.3f);
                Main.dust[dustSpawns].noGravity = true;
                Main.dust[dustSpawns].velocity *= 1.5f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                Projectile.numHits++;
                target.AddBuff(BuffID.Frostburn2, 180);
                target.AddBuff(CWRID.Buff_GlacialState, 30);

                Projectile.Explode(192, SoundID.Item27);
                SpanIceEffect();
            }

            Projectile.Kill();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.Explode(192, SoundID.Item27);
            SpanIceEffect(oldVelocity.ToRotation() - MathHelper.PiOver2);
            return base.OnTileCollide(oldVelocity);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
