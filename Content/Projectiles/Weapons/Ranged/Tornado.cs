using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class Tornado : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Ranged + "StormSurgeTornado";
        public override void SetDefaults() {
            Projectile.width = 66;
            Projectile.height = 66;
            Projectile.scale = 0.5f;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -2;
            Projectile.timeLeft = 90;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override void AI() {
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 5);
            Lighting.AddLight(Projectile.Center, 0f, 1.25f, 1.25f);
            if (Projectile.scale <= 3f) {
                Projectile.scale *= 1.03f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 155; i++) {
                Dust dust = Main.dust[Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.BlueTorch, 0, 13)];
                dust.velocity = (MathHelper.TwoPi / 155f * i).ToRotationVector2() * 23;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.spriteBatch.Draw(value, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(value, Projectile.frame, 6)
                , Projectile.GetAlpha(lightColor), Projectile.rotation, CWRUtils.GetOrig(value, 6), Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
