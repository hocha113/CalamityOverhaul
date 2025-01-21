using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    //钨子弹和火枪共用一个弹幕，这导致一些机制将钨子弹判定为火枪子弹，这个类新增加了专属于钨子弹的弹幕，用于解决这个问题
    internal class TungstenBulletProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 360;
            Projectile.extraUpdates = 3;
        }

        public override void AI() {
            PRT_Spark spark = new PRT_Spark(Projectile.Center, Projectile.velocity / 10, false, 4, 0.8f, Color.White);
            PRTLoader.AddParticle(spark);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 4; i++) {
                PRT_Spark spark = new PRT_Spark(Projectile.Center, CWRUtils.randVr(14, 26), false, 4, 0.8f, Color.White);
                PRTLoader.AddParticle(spark);
            }
        }

        public override void OnKill(int timeLeft) {
            
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            for (int i = 0; i < 4; i++) {
                PRT_Spark spark = new PRT_Spark(Projectile.Center, CWRUtils.randVr(14, 26), false, 4, 0.8f, Color.White);
                PRTLoader.AddParticle(spark);
            }
            Projectile.velocity = new Vector2(0, -Projectile.velocity.Y);
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            return false;
        }
    }
}
