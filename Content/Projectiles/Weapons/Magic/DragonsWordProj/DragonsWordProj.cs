using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Graphics.Metaballs;
using CalamityMod.Particles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.DragonsWordProj
{
    internal class DragonsWordProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        ref float Time => ref Projectile.ai[0];
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 18;
            Projectile.extraUpdates = 6;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
            Projectile.timeLeft = 1220 * Projectile.extraUpdates;
        }

        public override bool? CanHitNPC(NPC target) {
            if (Time < 150 * Projectile.extraUpdates) {
                return false;
            }
            return base.CanHitNPC(target);
        }

        public override bool PreAI() {
            Player Owner = Main.player[Projectile.owner];
            float targetDist = Vector2.Distance(Owner.Center, Projectile.Center);
            if (Time % 5 == 0 && Time > 35f && targetDist < 1400f) {
                SparkParticle spark = new SparkParticle(Projectile.Center + Main.rand.NextVector2Circular(1 + Time * 0.1f, 1 + Time * 0.1f)
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
