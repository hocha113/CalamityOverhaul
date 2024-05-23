using CalamityOverhaul.Content.Projectiles.Weapons.Melee.StormGoddessSpearProj;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.SparkProj
{
    internal class SparkLightning : StormArc
    {
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < Main.rand.Next(3, 5); i++) {
                int sparky = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.UnusedWhiteBluePurple, 0f, 0f, 100, default, 1f);
                Main.dust[sparky].scale += Main.rand.Next(50) * 0.01f;
                Main.dust[sparky].noGravity = true;
                Main.dust[sparky].velocity = Main.rand.NextVector2Unit() * Main.rand.Next(13, 15);
            }
            SoundEngine.PlaySound(SoundID.Item94, target.position);
        }
    }
}
