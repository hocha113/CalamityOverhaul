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

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            NPC thisBody = Main.npc[(int)Projectile.ai[1]];
            if (!thisBody.Alives()) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = thisBody.Center;
            Projectile.rotation = Projectile.velocity.ToRotation();
            CWRRef.SpawnDestroyerPRTEffect(thisBody, Projectile.localAI[0], Projectile.ai[0], IdleTime);
            Projectile.localAI[0]++;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isClient) {
                return;
            }

            NPC thisBody = Main.npc[(int)Projectile.ai[1]];
            Vector2 shootVelocity = Projectile.velocity.UnitVector();
            shootVelocity *= (CWRWorld.Death || CWRWorld.MasterMode) ? 12 : 9;
            if (CWRWorld.BossRush) {
                shootVelocity *= 1.6f;
            }

            int projectileType = ProjectileID.DeathLaser;

            switch (Projectile.ai[0] % 3) {
                case 0:
                    projectileType = ProjectileID.DeathLaser;
                    break;
                case 1:
                    projectileType = CWRID.Proj_DestroyerCursedLaser;
                    break;
                case 2:
                    projectileType = CWRID.Proj_DestroyerElectricLaser;
                    break;
            }
            //我真的非常厌恶这些莫名其妙的伤害计算，泰拉的伤害计算就是一堆非常庞大的垃圾堆
            int damage = CWRRef.GetProjectileDamage(thisBody, projectileType);
            //仅在启用 EarlyHardmodeProgressionRework 且非 BossRush 模式时调整伤害
            if (CWRRef.GetEarlyHardmodeProgressionReworkBool() && !CWRRef.GetBossRushActive()) {
                //计算击败的机械 Boss 数量
                int downedMechBosses = (NPC.downedMechBoss1 ? 1 : 0) + (NPC.downedMechBoss2 ? 1 : 0) + (NPC.downedMechBoss3 ? 1 : 0);
                //根据击败的机械 Boss 数量调整伤害
                if (downedMechBosses == 0) {
                    damage = (int)(damage * 0.9f);
                }
                else if (downedMechBosses == 1) {
                    damage = (int)(damage * 0.95f);
                }
                //如果击败了 2 个或更多机械 Boss，不调整伤害
            }

            int proj = Projectile.NewProjectile(thisBody.GetSource_FromAI(), thisBody.Center
                , shootVelocity, projectileType, damage, 0f, Main.myPlayer, 1f, 0f);
            Main.projectile[proj].timeLeft = 600;
            Main.projectile[proj].netUpdate = true;
        }
    }
}
