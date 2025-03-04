using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic
{
    internal class PhaseBlasting : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        internal float getMode() {
            int level = InWorldBossPhase.Instance.SHPC_Level();
            return 1f + level * 0.08f;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float size = 62f + (160f * (getMode() - 1f));
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, size, targetHitbox);
        }

        public override void AI() {
            float mode = getMode();
            float lights = Main.rand.Next(30, 62) * 0.01f;
            lights *= Main.essScale;
            lights *= mode;
            Lighting.AddLight(Projectile.Center, 5f * lights, 1f * lights, 4f * lights);

            float projTimer = 25f;
            if (Projectile.ai[0] > 180f) {
                projTimer -= (Projectile.ai[0] - 180f) / 2f;
            }
            if (projTimer <= 0f) {
                projTimer = 0f;
                Projectile.Kill();
            }
            projTimer *= 0.7f;
            Projectile.ai[0] += 4f;

            Color color = Color.Blue;
            int randomDust = Main.rand.Next(3);
            if (randomDust == 0) {
                randomDust = DustID.FireworkFountain_Blue;
            }
            else if (randomDust == 1) {
                color = Color.Red;
                randomDust = DustID.FireworkFountain_Red;
            }
            else {
                color = Color.Yellow;
                randomDust = DustID.FireworkFountain_Yellow;
            }

            for (int i = 0; i < 8; i++) {
                Vector2 randOffset = Main.rand.NextVector2CircularEdge(64, 64);
                Vector2 spawnPos = Projectile.Center + randOffset * mode;
                Vector2 velocity = spawnPos.DirectionTo(Projectile.Center) * 4 * mode;
                if (Main.zenithWorld) {
                    PRT_Spark spark = new PRT_Spark(spawnPos, velocity, false, (int)(32 * mode), 1, color);
                    PRTLoader.AddParticle(spark);
                }
                else {
                    Dust d = Dust.NewDustPerfect(spawnPos, randomDust, velocity, Scale: 2);
                    d.noGravity = true;
                    d.noLight = true;
                }
            }

            PRT_LavaFire lavaFire = new PRT_LavaFire {
                Velocity = Vector2.Zero,
                Position = Projectile.Center,
                Scale = Main.rand.NextFloat(0.8f, 1.2f) * mode,
                Color = Color.White
            };
            lavaFire.ai[0] = 1;
            lavaFire.ai[1] = 1;
            lavaFire.minLifeTime = 112;
            lavaFire.maxLifeTime = 218;
            lavaFire.colors = new Color[3];
            lavaFire.colors[0] = new Color(255, 180, 60, 255);
            lavaFire.colors[1] = new Color(220, 120, 40, 255);
            lavaFire.colors[2] = new Color(190, 80, 30, 255);
            PRTLoader.AddParticle(lavaFire);
        }
    }
}
