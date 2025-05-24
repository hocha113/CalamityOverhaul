using CalamityMod.Particles;
using CalamityMod.Projectiles.Boss;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.Destroyer
{
    internal class SpawnLaserEffect : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        internal const int IdleTime = 100;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.tileCollide = false;
            Projectile.timeLeft = IdleTime;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0 && !Main.dedServ) {
                NPC thisBody = Main.npc[(int)Projectile.ai[1]];
                Color telegraphColor;
                Particle spark;

                switch (Projectile.ai[0] % 3) {
                    case 0:
                        telegraphColor = Color.Red;
                        spark = new DestroyerReticleTelegraph(thisBody, telegraphColor, 1.5f, 0.15f, IdleTime + 20);
                        GeneralParticleHandler.SpawnParticle(spark);
                        break;
                    case 1:
                        telegraphColor = Color.Green;
                        spark = new DestroyerSparkTelegraph(thisBody, telegraphColor * 2f, Color.White, 3f, IdleTime + 20,
                                Main.rand.NextFloat(MathHelper.ToRadians(3f)) * Main.rand.NextBool().ToDirectionInt());
                        GeneralParticleHandler.SpawnParticle(spark);
                        break;
                    case 2:
                        telegraphColor = Color.Cyan;
                        spark = new DestroyerSparkTelegraph(thisBody, telegraphColor * 2f, Color.White, 3f, IdleTime + 20,
                                Main.rand.NextFloat(MathHelper.ToRadians(3f)) * Main.rand.NextBool().ToDirectionInt());
                        GeneralParticleHandler.SpawnParticle(spark);
                        break;
                }
            }
            Projectile.localAI[0]++;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isClient) {
                return;
            }

            NPC thisBody = Main.npc[(int)Projectile.ai[1]];
            Player player = Main.player[(int)Projectile.ai[2]];
            Vector2 shootVelocity = thisBody.Center.To(player.Center).UnitVector();
            shootVelocity *= (DestroyerHeadAI.Death || DestroyerHeadAI.MasterMode) ? 12 : 9;
            if (DestroyerHeadAI.BossRush) {
                shootVelocity *= 1.6f;
            }

            int projectileType = ProjectileID.DeathLaser;

            switch (Projectile.ai[0] % 3) {
                case 0:
                    projectileType = ProjectileID.DeathLaser;
                    break;
                case 1:
                    projectileType = ModContent.ProjectileType<DestroyerCursedLaser>();
                    break;
                case 2:
                    projectileType = ModContent.ProjectileType<DestroyerElectricLaser>();
                    break;
            }

            int proj = Projectile.NewProjectile(thisBody.GetSource_FromAI(), thisBody.Center
                , shootVelocity, projectileType, thisBody.damage / 2, 0f, Main.myPlayer, 1f, 0f);
            Main.projectile[proj].timeLeft = 1200;
        }
    }
}
