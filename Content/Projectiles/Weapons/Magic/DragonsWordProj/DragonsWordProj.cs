using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.DragonsWordProj
{
    internal class DragonsWordProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private ref float Time => ref Projectile.ai[0];
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
            return Time < 150 * Projectile.extraUpdates ? false : base.CanHitNPC(target);
        }

        public override bool PreAI() {
            Player Owner = Main.player[Projectile.owner];
            float targetDist = Vector2.Distance(Owner.Center, Projectile.Center);
            if (!VaultUtils.isServer) {
                float OrbSize = Main.rand.NextFloat(0.5f, 0.8f);
                var orb = new PRT_Bloomlight(Projectile.Center, Vector2.Zero, Color.OrangeRed, OrbSize + 0.6f, 8);
                PRTLoader.AddParticle(orb);
                var orb2 = new PRT_Bloomlight(Projectile.Center, Vector2.Zero, Color.White, OrbSize + 0.2f, 8);
                PRTLoader.AddParticle(orb2);
                if (Time % 5 == 0 && Time > 35f && targetDist < 1400f) {
                    PRT_Spark spark = new PRT_Spark(Projectile.Center + Main.rand.NextVector2Circular(1 + Time * 0.1f, 1 + Time * 0.1f)
                        , -Projectile.velocity * 0.5f, false, 15, Main.rand.NextFloat(0.4f, 0.7f), Main.rand.NextBool() ? Color.DarkOrange : Color.OrangeRed);
                    PRTLoader.AddParticle(spark);
                }
            }
            if (Time > 160 * Projectile.extraUpdates) {
                NPC target = Projectile.Center.FindClosestNPC(1600);
                if (target != null) {
                    if (Time < 290 * Projectile.extraUpdates) {
                        Projectile.SmoothHomingBehavior(target.Center, 1, 0.08f);
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
            target.AddBuff(CWRID.Buff_Dragonfire, 420);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 16; i++) {
                float OrbSize = Main.rand.NextFloat(1.5f, 1.8f);
                var orb = new PRT_Bloomlight(Projectile.Center, Vector2.Zero, Color.OrangeRed, OrbSize + 0.6f, 8);
                PRTLoader.AddParticle(orb);
                var orb2 = new PRT_Bloomlight(Projectile.Center, Vector2.Zero, Color.White, OrbSize + 0.2f, 8);
                PRTLoader.AddParticle(orb2);
                PRT_Spark spark = new PRT_Spark(Projectile.Center + Main.rand.NextVector2Circular(11 + Time * 0.1f, 11 + Time * 0.1f)
                        , -Projectile.velocity * 0.5f, false, 15, Main.rand.NextFloat(0.4f, 0.7f), Main.rand.NextBool() ? Color.DarkOrange : Color.OrangeRed);
                PRTLoader.AddParticle(spark);
            }

            Projectile.Explode();
        }
    }
}
