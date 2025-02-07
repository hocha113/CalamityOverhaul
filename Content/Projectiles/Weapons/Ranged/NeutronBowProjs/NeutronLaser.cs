using CalamityMod;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.NeutronBowProjs
{
    internal class NeutronLaser : ModProjectile
    {
        public override string Texture => "CalamityMod/Projectiles/LaserProj";
        public override void SetDefaults() {
            Projectile.CloneDefaults(ModContent.ProjectileType<DrataliornusExoArrow>());
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 1;
            Projectile.ArmorPenetration = 80;
        }
        public override void AI() {
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 25;
            }
            if (Projectile.alpha < 0) {
                Projectile.alpha = 0;
            }
            float inc = 3f;
            if (Projectile.ai[1] == 0f) {
                Projectile.localAI[0] += inc;
                if (Projectile.localAI[0] > 100f) {
                    Projectile.localAI[0] = 100f;
                }
            }
            else {
                Projectile.localAI[0] -= inc;
                if (Projectile.localAI[0] <= 0f) {
                    Projectile.Kill();
                    return;
                }
            }
            BasePRT spark = new PRT_Spark(Projectile.Center
                        , Projectile.velocity, false, 10, Main.rand.NextFloat(1.2f, 2.3f), Color.BlueViolet);
            PRTLoader.AddParticle(spark);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.immune[Projectile.owner] = 0;
        public override Color? GetAlpha(Color lightColor) => new Color(0, 125, 210, Projectile.alpha);
        public override bool PreDraw(ref Color lightColor) => Projectile.DrawBeam(200f, 3f, Color.White);
    }
}
