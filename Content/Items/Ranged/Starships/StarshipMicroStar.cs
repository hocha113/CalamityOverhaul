using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Starships
{
    //从枪口上下释放的小型群星弹幕
    internal class StarshipMicroStar : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder2;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.YellowStarDust
                    , Main.rand.NextVector2Circular(1f, 1f), 100, default, 0.8f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.3f, 0.3f, 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float f = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                Vector2 p = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                sb.Draw(glow, p, null, new Color(255, 200, 120, 0) * f * 0.45f, 0f
                    , glow.Size() * 0.5f, new Vector2(0.45f + f * 0.8f, 0.3f + f * 0.2f), SpriteEffects.None, 0);
            }

            sb.Draw(glow, drawPos, null, new Color(255, 220, 160, 0) * 0.9f, 0f, glow.Size() * 0.5f, 0.45f, SpriteEffects.None, 0);
            if (star != null) {
                sb.Draw(star, drawPos, null, new Color(255, 230, 180, 0) * 0.9f
                    , Main.GlobalTimeWrappedHourly * 8f, star.Size() * 0.5f, 0.3f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
