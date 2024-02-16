using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class GunCasing : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "GunCasing";
        public int Time { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }

        public override void SetDefaults() {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.damage = 10;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = true;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.scale = 1.2f;
            Projectile.light = 0.3f;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            Time++;
            Projectile.rotation += MathHelper.ToRadians(Projectile.velocity.X * 13);
            Projectile.velocity += new Vector2(0, 0.1f);

            if (Time % 13 == 0)
                Dust.NewDust(Projectile.Center, 3, 3, DustID.Smoke, Projectile.velocity.X, Projectile.velocity.Y);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(CWRSound.Case, Projectile.position);
        }
    }
}
