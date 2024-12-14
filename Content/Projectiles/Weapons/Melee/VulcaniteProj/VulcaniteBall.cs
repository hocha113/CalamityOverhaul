using CalamityMod.Particles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.VulcaniteProj
{
    internal class VulcaniteBall : ModProjectile
    {
        public override string Texture => CWRConstant.Other + "HellFire";

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 320;
        }

        public override bool? CanDamage() {
            return Projectile.timeLeft > 220 ? false : base.CanDamage();
        }

        public override void AI() {
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 3);
            Projectile.rotation += 0.3f;
            Projectile.velocity *= 0.99f;
            if (Main.rand.NextBool(3))
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Firework_Red, 0, -1);
            for (int i = 0; i < 3; i++) {
                bool LowVel = Main.rand.NextBool() ? false : true;
                FlameParticle ballFire = new FlameParticle(Projectile.Center + CWRUtils.randVr(13), Main.rand.Next(13, 22), Main.rand.NextFloat(0.1f, 0.22f), Main.rand.NextFloat(0.02f, 0.07f), Color.Gold, Color.DarkRed) {
                    Velocity = new Vector2(Projectile.velocity.X * 0.8f, -10).RotatedByRandom(0.005f) * (LowVel ? Main.rand.NextFloat(0.4f, 0.65f) : Main.rand.NextFloat(0.8f, 1f))
                };
                GeneralParticleHandler.SpawnParticle(ballFire);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(value, Projectile.frame, 4), Color.White
                , 0, CWRUtils.GetOrig(value, 4), Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
