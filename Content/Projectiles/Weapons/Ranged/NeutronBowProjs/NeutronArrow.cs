using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.NeutronBowProjs
{
    internal class NeutronArrow : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "NeutronArrow2";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 120;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
            Projectile.MaxUpdates = 3;
            Projectile.penetrate = 18;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 1;
            Projectile.ArmorPenetration = 80;

        }

        public override void AI() {
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 25;
            }
            Projectile.SetArrowRot();
            Lighting.AddLight(Projectile.Center + Projectile.velocity, Color.AntiqueWhite.ToVector3());
            if (++Projectile.ai[0] > 10 || ++Projectile.ai[1] > 2) {
                Vector2 norl = Projectile.velocity.GetNormalVector();
                float sengs = Projectile.timeLeft * 0.01f;
                for (int j = 0; j < 53; j++) {
                    BasePRT spark = new PRT_HeavenfallStar(Projectile.Center
                        , norl * (0.1f + j * 0.34f) * sengs, false, 20, Main.rand.NextFloat(0.6f, 1.3f), Color.BlueViolet);
                    PRTLoader.AddParticle(spark);
                }
                for (int j = 0; j < 53; j++) {
                    BasePRT spark = new PRT_HeavenfallStar(Projectile.Center
                        , norl * -(0.1f + j * 0.34f) * sengs, false, 20, Main.rand.NextFloat(0.6f, 1.3f), Color.BlueViolet);
                    PRTLoader.AddParticle(spark);
                }
                Projectile.ai[0] = 0;
                Projectile.ai[1] = -1000;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits > 0) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Vector2 rand = VaultUtils.RandVr(560, 780);
                Vector2 vr = rand.UnitVector() * -20;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center + rand
                        , vr, ModContent.ProjectileType<NeutronLaser>(), Projectile.damage * 2, 0);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center
                        , Vector2.Zero, ModContent.ProjectileType<NeutronExplosionRanged>(), Projectile.damage * 2, 0);
            }
        }
    }
}
