using CalamityOverhaul.Content.Items.Tools;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Tools
{
    internal class SpanDMBall : ModProjectile
    {
        public override string Texture => CWRConstant.Item + "Tools/DarkMatter";
        internal DarkMatterBall darkMatterBall;
        private Player Owner => Main.player[Projectile.owner];
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 130;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.hostile = false;
        }

        public override void AI() {
            if (Projectile.ai[0] > 60) {
                Projectile.ChasingBehavior(Owner.Center, 13);
                Projectile.Center = Vector2.Lerp(Projectile.Center, Owner.Center, 0.04f);
                if (Projectile.Distance(Owner.Center) < Projectile.width) {
                    Projectile.Kill();
                }
                Projectile.scale -= 0.02f;
            }
            else {
                Projectile.rotation += 0.1f;
                Projectile.scale += 0.02f;
                Projectile.alpha += 5;
                if (Projectile.alpha > 255) {
                    Projectile.alpha = 255;
                }
            }
            Projectile.ai[0]++;
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.QuickSpawnItem(Owner.FromObjectGetParent(), darkMatterBall.Item, 1);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float alp = Projectile.alpha / 255f;

            if (Projectile.ai[1] != 1) {
                Vector2 drawPos = Projectile.Center - Main.screenPosition;
                Color drawColor = Color.White * alp * 0.02f;
                Vector2 drawOrig = DarkMatterBall.DarkMatter.Size() / 2;
                float slp = (255 - Projectile.alpha) / 15f;
                for (int i = 0; i < 113; i++) {
                    Main.EntitySpriteDraw(DarkMatterBall.DarkMatter.Value, drawPos, null, drawColor
                    , Projectile.rotation + (MathHelper.TwoPi / 113 * i), drawOrig, slp, SpriteEffects.None, 0);
                }
            }

            Main.EntitySpriteDraw(DarkMatterBall.DarkMatter.Value, Projectile.Center - Main.screenPosition, null, Color.White * alp
                , Projectile.rotation, DarkMatterBall.DarkMatter.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
