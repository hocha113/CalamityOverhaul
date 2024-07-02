using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class DeadWave : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Wave";

        public int Status { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        public int Behavior { get => (int)Projectile.ai[1]; set => Projectile.ai[1] = value; }
        public int Time { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.scale = 1;
            Projectile.alpha = 205;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.MaxUpdates = 3;
            Projectile.timeLeft = 150 * Projectile.MaxUpdates;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.scale += 0.01f;
            if (Projectile.timeLeft < 200)
                Projectile.alpha -= 1;
            if (Projectile.alpha <= 0)
                Projectile.Kill();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 overs = Projectile.velocity.GetNormalVector();
            float wit = 30 * Projectile.scale;
            float point = 0;
            return Collision.CheckAABBvLineCollision
                (targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center + overs * wit, Projectile.Center + overs * -wit, 3, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = ModContent.Request<Texture2D>(Texture).Value;
            float alp = Projectile.alpha / 255f;
            Color colors = Color.Lerp(Color.White, new Color(22, 72, 252), alp) * alp;
            Main.spriteBatch.SetAdditiveState();
            Main.EntitySpriteDraw(
                value,
                Projectile.Center - Main.screenPosition,
                null,
                colors,
                Projectile.rotation,
                value.Size() / 2f,
                Projectile.scale,
                SpriteEffects.FlipHorizontally,
                0
                );
            Main.spriteBatch.ResetBlendState();
            return false;
        }
    }
}
