using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic
{
    internal class AetherOrb2 : AetherOrb
    {
        public override void SetDefaults() {
            base.SetDefaults();
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
        }

        public override void AI() {
            base.AI();
            if (Time > 60) {
                NPC target = Projectile.Center.FindClosestNPC(600);
                if (target != null) {
                    Projectile.ChasingBehavior2(target.Center, 1.01f, 0.3f);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitNPC(target, hit, damageDone);
        }

        public override void OnKill(int timeLeft) {
        }
    }
}
