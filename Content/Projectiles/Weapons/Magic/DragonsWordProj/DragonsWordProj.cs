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
            CWRRef.DragonsWordEffect(true, Projectile, Time, targetDist);
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
            CWRRef.DragonsWordEffect(true, Projectile, Time, 0);
            Projectile.Explode();
        }
    }
}
