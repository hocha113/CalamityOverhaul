using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons
{
    internal class PebbleProj : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "PebbleProj";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.DamageType = DamageClass.Throwing;
            Projectile.timeLeft = 390;
            Projectile.friendly = true;
        }

        public override void AI() {
            Projectile.velocity += new Vector2(Projectile.velocity.X * -0.012f, 0.13f);
            Projectile.rotation += Projectile.velocity.X * 0.1f;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item50 with { Pitch = -0.2f }, Projectile.position);
            for (int i = 0; i < 9; i++) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                    , DustID.Copper, -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f, 120, default, 1.5f);
            }
        }
    }
}
