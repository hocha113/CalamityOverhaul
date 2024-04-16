using CalamityOverhaul.Common;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class PebbleBall : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "PebbleBall";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.timeLeft = 165;
        }

        public override void AI() {
            Projectile.rotation += Projectile.velocity.X * 0.1f;
            Projectile.velocity.Y += 0.1f;
        }
    }
}
