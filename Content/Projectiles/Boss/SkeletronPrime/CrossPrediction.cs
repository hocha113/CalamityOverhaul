using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    internal class CrossPrediction : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";
        private int scaleTimer = 0;
        private int scaleIndex = 0;
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.scale = 1f;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 50;
            Projectile.alpha = 0;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0) {
                Projectile.rotation = Projectile.velocity.ToRotation();
                Projectile.velocity = Vector2.Zero;
            }

            Projectile.scale += 0.05f;
            if (Projectile.alpha < 255) {
                Projectile.alpha += 15;
            }

            if (scaleTimer < 8 && scaleIndex == 0) {
                scaleTimer++;
            }
            if (Projectile.timeLeft < 30) {
                scaleIndex = 1;
            }
            if (scaleIndex > 0) {
                if (--scaleTimer <= 0) {
                    Projectile.Kill();
                }
            }

            Projectile.localAI[0]++;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (scaleTimer < 0) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Color drawColor = Color.White;
            drawColor.A = 0;
            for (int i = 0; i < 4; i++) {
                Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, drawColor
                    , Projectile.rotation + MathHelper.PiOver2 * i
                , new Vector2(0, tex.Height / 2f), new Vector2(1000, scaleTimer * 0.04f), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
