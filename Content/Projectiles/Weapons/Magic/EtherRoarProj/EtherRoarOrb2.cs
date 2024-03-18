using Microsoft.Xna.Framework;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.EtherRoarProj
{
    internal class EtherRoarOrb2 : EtherRoarOrb
    {
        public override void SetDefaults() {
            base.SetDefaults();
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            InnerColor = new Color(Main.DiscoR, 100, 200);
            base.AI();
            if (Time > 60) {
                NPC target = Projectile.Center.FindClosestNPC(600);
                if (target != null) {
                    Projectile.ChasingBehavior2(target.Center, 1.01f, 0.35f);
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
