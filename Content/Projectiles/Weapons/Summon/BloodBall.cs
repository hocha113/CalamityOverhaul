using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Summon
{
    internal class BloodBall : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.timeLeft = 180;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Dust dust = Dust.NewDustDirect(Projectile.position, 13, 13
                , DustID.Blood, Projectile.velocity.X / 5, Projectile.velocity.Y / 5);
            dust.scale = Main.rand.NextFloat(1, 1.2f);
        }

        public override void OnKill(int timeLeft) {
            for (float i = 0; i < MathHelper.TwoPi; i += 0.05f) {
                Vector2 vr = i.ToRotationVector2() * Main.rand.Next(12, 15);
                Dust dust = Dust.NewDustDirect(Projectile.position, 32, 32
                , DustID.Blood, vr.X, vr.Y);
                dust.scale = Main.rand.NextFloat(1, 1.2f);
            }

            Projectile.penetrate = -1;
            Projectile.width = 320;
            Projectile.height = 320;
            Projectile.Center = Projectile.position;
            Projectile.Damage();
        }
    }
}
