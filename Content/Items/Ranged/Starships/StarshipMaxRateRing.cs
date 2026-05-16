using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Starships
{
    //群星巨舰达到最高射速时在枪身释放的星辰光环演出
    internal class StarshipMaxRateRing : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.hide = true;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            Projectile.localAI[0]++;
            Player owner = Main.player[Projectile.owner];
            if (owner.active && !owner.dead) {
                Projectile.Center = owner.Center;
            }

            //辐射粒子
            if (Projectile.localAI[0] < 30) {
                for (int i = 0; i < 3; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(6f, 11f);
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.PinkStarfish, vel, 80, default, 1.5f);
                    d.noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D diffuse = CWRAsset.DiffusionCircle?.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float t = 1f - Projectile.timeLeft / 60f;
            float radius = t * 220f;
            float alpha = MathHelper.Clamp(1f - t, 0f, 1f);

            //环状光环
            if (diffuse != null) {
                sb.Draw(diffuse, drawPos, null, new Color(140, 190, 255, 0) * alpha * 0.9f, 0f
                    , diffuse.Size() * 0.5f, radius / diffuse.Width * 2f, SpriteEffects.None, 0);
            }
            sb.Draw(glow, drawPos, null, new Color(200, 220, 255, 0) * alpha, 0f, glow.Size() * 0.5f, 1.2f + t * 2.5f, SpriteEffects.None, 0);
            return false;
        }
    }
}
