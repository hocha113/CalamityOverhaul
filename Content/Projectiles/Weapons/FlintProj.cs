using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons
{
    internal class FlintProj : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "FlintProj";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.DamageType = DamageClass.Throwing;
            Projectile.timeLeft = 390;
            Projectile.friendly = true;
        }

        public override void AI() {
            Projectile.velocity += new Vector2(Projectile.velocity.X * -0.01f, 0.12f);
            Projectile.rotation += Projectile.velocity.X * 0.1f;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item50 with { Pitch = 0.3f }, Projectile.position);
            int dust_splash = 0;
            while (dust_splash < 9) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                    , DustID.Copper, -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f, 120, default, 1.5f);
                dust_splash += 1;
            }
        }
    }
}
