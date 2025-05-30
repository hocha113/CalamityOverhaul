using CalamityMod.Events;
using CalamityMod.NPCs;
using CalamityMod;
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
            //我真的非常厌恶这些莫名其妙的伤害计算，泰拉的伤害计算就是一堆非常庞大的垃圾堆
            int damage = thisBody.GetProjectileDamage(projectileType);
            //仅在启用 EarlyHardmodeProgressionRework 且非 BossRush 模式时调整伤害
            if (CalamityConfig.Instance.EarlyHardmodeProgressionRework && !BossRushEvent.BossRushActive) {
                //计算击败的机械 Boss 数量
                int downedMechBosses = (NPC.downedMechBoss1 ? 1 : 0) + (NPC.downedMechBoss2 ? 1 : 0) + (NPC.downedMechBoss3 ? 1 : 0);
                //根据击败的机械 Boss 数量调整伤害
                if (downedMechBosses == 0) {
                    damage = (int)(damage * CalamityGlobalNPC.EarlyHardmodeProgressionReworkFirstMechStatMultiplier_Expert);
                }
                else if (downedMechBosses == 1) {
                    damage = (int)(damage * CalamityGlobalNPC.EarlyHardmodeProgressionReworkSecondMechStatMultiplier_Expert);
                }
                //如果击败了 2 个或更多机械 Boss，不调整伤害
            }

            int proj = Projectile.NewProjectile(thisBody.GetSource_FromAI(), thisBody.Center
                , shootVelocity, projectileType, damage, 0f, Main.myPlayer, 1f, 0f);
            Main.projectile[proj].timeLeft = 1200;
        }
    }
}
