using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs
{
    internal class GraniteDartHeld : BaseThrowable
    {
        public override string Texture => CWRConstant.Item + "Rogue/GraniteDart";
        public override void SetThrowable() => OffsetRoting = 45 * CWRUtils.atoR;
        public override void FlyToMovementAI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (++Projectile.ai[2] > 36) {
                Projectile.velocity *= 0.92f;
                Projectile.velocity.Y += 0.65f;
            }
        }

        public override bool PreThrowOut() {
            SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.velocity.UnitVector() * 8;
            Projectile.velocity = UnitToMouseV * 17.5f;
            Projectile.tileCollide = true;
            Projectile.extraUpdates += 1;
            if (stealthStrike) {
                Projectile.damage *= 2;
                Projectile.ArmorPenetration = 10;
                Projectile.penetrate = 6;
                Projectile.extraUpdates = 3;
                Projectile.scale = 1.5f;
            }
            return false;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = oldVelocity * 0.6f;
            Projectile.alpha -= 15;
            return false;
        }
    }
}
