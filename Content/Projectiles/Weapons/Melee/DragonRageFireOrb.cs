using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Graphics.Metaballs;
using CalamityMod.Particles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class DragonRageFireOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        ref float Time => ref Projectile.ai[0];
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 2;
            Projectile.extraUpdates = 6;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 1220 * Projectile.extraUpdates;
        }

        public override bool? CanHitNPC(NPC target) {
            if (Time < 15 * Projectile.extraUpdates) {
                return false;
            }
            return base.CanHitNPC(target);
        }

        public override bool PreAI() {
            Player Owner = Main.player[Projectile.owner];
            float targetDist = Vector2.Distance(Owner.Center, Projectile.Center);
            if (Time == 0) {
                for (int i = 0; i <= 7; i++) {
                    int DustType1 = 259;
                    Vector2 dustyVelocity = Projectile.velocity;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool(3) ? DustType1 : 174);
                    dust.scale = dust.type == DustType1 ? Main.rand.NextFloat(0.9f, 1.9f) : Main.rand.NextFloat(0.8f, 1.7f);
                    dust.velocity = dustyVelocity.RotatedByRandom(0.5f) * Main.rand.NextFloat(0.3f, 0.8f);
                    dust.noGravity = true;
                    Dust dust2 = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool(3) ? DustType1 : 174);
                    dust2.scale = dust.type == DustType1 ? Main.rand.NextFloat(0.85f, 1.8f) : Main.rand.NextFloat(0.75f, 1.6f);
                    dust2.velocity = dustyVelocity.RotatedByRandom(0.15f) * Main.rand.NextFloat(0.8f, 2.1f);
                    dust2.noGravity = true;
                }
                for (int i = 0; i <= 5; i++) {
                    Vector2 smokeVel = Projectile.velocity * 4.2f;
                    float rotationAngle = Main.rand.NextFloat(-0.15f, 0.15f);
                    smokeVel = smokeVel.RotatedBy(rotationAngle) * Main.rand.NextFloat(0.45f, 1.3f);

                    float smokeScale = Main.rand.NextFloat(0.4f, 1.2f);

                    SmallSmokeParticle smoke = new SmallSmokeParticle(Projectile.Center, smokeVel, Color.DimGray, Main.rand.NextBool() ? Color.SlateGray : Color.Black, smokeScale, 100);
                    GeneralParticleHandler.SpawnParticle(smoke);
                }
            }
            if (Time % 5 == 0 && Time > 35f && targetDist < 1400f) {
                SparkParticle spark = new SparkParticle(Projectile.Center + Main.rand.NextVector2Circular(1 + (Time * 0.1f), 1 + (Time * 0.1f))
                    , -Projectile.velocity * 0.5f, false, 15, Main.rand.NextFloat(0.4f, 0.7f), Main.rand.NextBool() ? Color.DarkOrange : Color.OrangeRed);
                GeneralParticleHandler.SpawnParticle(spark);
            }
            if (targetDist < 1400f) {
                ModContent.GetInstance<DragonsBreathFlameMetaball2>().SpawnParticle(Projectile.Center, Time * 0.1f + 0.2f);
                ModContent.GetInstance<DragonsBreathFlameMetaball>().SpawnParticle(Projectile.Center + Projectile.velocity, Time * 0.09f + 0.15f);
            }
            if (Time > 160 * Projectile.extraUpdates) {
                NPC target = Projectile.Center.FindClosestNPC(1600);
                if (target != null) {
                    if (Time < 290 * Projectile.extraUpdates) {
                        Projectile.ChasingBehavior2(target.Center, 1, 0.08f);
                    }
                    else {
                        Projectile.ChasingBehavior(target.Center, Projectile.velocity.Length());
                    }
                }
            }
            else {
                Projectile.velocity = Projectile.velocity.RotatedBy(Projectile.ai[1]);
            }
            Time++;
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<Dragonfire>(), 420);
        }

        public override void OnKill(int timeLeft) {
            float OrbSize = Main.rand.NextFloat(0.5f, 0.8f);
            Particle orb = new GenericBloom(Projectile.Center, Vector2.Zero, Color.OrangeRed, OrbSize + 0.6f, 8, true);
            GeneralParticleHandler.SpawnParticle(orb);
            Particle orb2 = new GenericBloom(Projectile.Center, Vector2.Zero, Color.White, OrbSize + 0.2f, 8, true);
            GeneralParticleHandler.SpawnParticle(orb2);
            Projectile.Explode();
        }
    }
}
