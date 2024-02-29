using Microsoft.Xna.Framework;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DawnshatterAzureProj
{
    internal class TheDaybreak2 : TheDaybreak
    {
        public override void AI() {
            CWRUtils.ClockFrame(ref Projectile.frame, 6, 3);
            Lighting.AddLight(Projectile.Center, Color.Red.ToVector3() * 3f);
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;
            Projectile.velocity *= 0.99f;
        }
    }
}
