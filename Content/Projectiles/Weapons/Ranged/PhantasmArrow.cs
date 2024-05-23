using CalamityMod.Particles;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class PhantasmArrow : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "PhantasmArrow";
        private ref float Time => ref Projectile.ai[0];
        private Color InnerColor => new Color(24, 93, 66);
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.tileCollide = Projectile.ignoreWater = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.MaxUpdates = 3;
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;
            NPC target = Main.npc[(int)Projectile.ai[1]];
            if (target.Alives()) {
                Projectile.ChasingBehavior(target.Center, 23);
            }
            else {
                target = Projectile.Center.FindClosestNPC(1600);
                if (target != null) {
                    Projectile.ChasingBehavior2(target.Center, 1, 0.06f);
                }
            }
            Lighting.AddLight(Projectile.Center, InnerColor.ToVector3() * 0.2f);
            Player Owner = Main.player[Projectile.owner];
            float targetDist = Vector2.Distance(Owner.Center, Projectile.Center);
            if (Projectile.timeLeft % 2 == 0 && Time > 5f && targetDist < 1400f) {
                AltSparkParticle spark = new(Projectile.Center, Projectile.velocity * 0.05f, false, 12, 2.3f, Color.AntiqueWhite);
                GeneralParticleHandler.SpawnParticle(spark);
            }
            if (Main.rand.NextBool(3) && Time > 5f && targetDist < 1400f) {
                Particle orb = new GenericBloom(Projectile.Center + Main.rand.NextVector2Circular(10, 10), Projectile.velocity * Main.rand.NextFloat(0.05f, 0.5f), Color.DarkOliveGreen, Main.rand.NextFloat(0.2f, 0.45f), Main.rand.Next(9, 12), true, false);
                GeneralParticleHandler.SpawnParticle(orb);
            }
            if (Projectile.timeLeft % 3 == 0 && Time > 5f && targetDist < 1400f) {
                LineParticle spark2 = new(Projectile.Center, -Projectile.velocity * 0.05f, false, 17, 1.7f, InnerColor);
                GeneralParticleHandler.SpawnParticle(spark2);
            }
            Time++;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Time < 3) {
                return false;
            }
            return base.PreDraw(ref lightColor);
        }
    }
}
