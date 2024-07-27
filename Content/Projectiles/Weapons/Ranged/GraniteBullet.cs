using CalamityOverhaul.Common.Effects;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class GraniteBullet : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 6;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.light = 0.3f;
            Projectile.timeLeft = 90;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.extraUpdates = 3;
        }

        public override void AI() {
            CWRDust.SplashDust(Projectile, 6, DustID.BlueTorch, DustID.BlueTorch
                , 0, Color.DarkBlue, EffectLoader.StreamerDustShader);
        }

        public override void OnKill(int timeLeft) {
            Projectile.rotation = Projectile.velocity.ToRotation();
            CWRDust.SplashDust(Projectile, 36, DustID.BlueTorch, DustID.BlueTorch
                , 16, Color.DarkBlue, EffectLoader.StreamerDustShader);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = oldVelocity.RotatedByRandom(0.45f);
            Projectile.penetrate = -1;
            return false;
        }
    }
}
