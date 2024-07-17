using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs
{
    internal class MarbleDaggerHeld : BaseThrowable
    {
        public override string Texture => CWRConstant.Item + "Rogue/MarbleDagger";
        public override void FlyToMovementAI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.timeLeft < 200) {
                Projectile.velocity.Y += 0.6f;
            }
        }

        public override bool PreThrowOut() {
            SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.velocity.UnitVector() * 8;
            Projectile.tileCollide = true;
            if (Projectile.ai[2] > 0) {
                Projectile.extraUpdates += 1;
            }
            if (stealthStrike) {
                Projectile.damage *= 2;
                Projectile.ArmorPenetration = 10;
                Projectile.penetrate = 6;
                Projectile.extraUpdates = 3;
                Projectile.scale = 1.5f;
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.ai[2] == 0) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center
                    , UnitToMouseV.RotatedBy(0.15f) * 6, Type, Projectile.damage
                    , Projectile.knockBack, Owner.whoAmI, 0, 0, 1);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center
                    , UnitToMouseV.RotatedBy(-0.15f) * 6, Type, Projectile.damage
                    , Projectile.knockBack, Owner.whoAmI, 0, 0, 1);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = oldVelocity * 0.6f;
            Projectile.alpha -= 15;
            return false;
        }
    }
}
