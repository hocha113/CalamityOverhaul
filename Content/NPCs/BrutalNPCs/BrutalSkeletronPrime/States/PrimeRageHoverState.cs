using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 狂暴悬停：三阶段常态。失去四肢的头颅亲自压制，
    /// 交替吐出死亡激光扇面与追踪火箭散射；双子皆灭时机体持续过载漏血，
    /// 战斗自带倒计时的紧迫感。三轮弹幕后按固定序列切换强力招式。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.RageHover, typeof(PrimeStateContext))]
    internal class PrimeRageHoverState : PrimeStateBase
    {
        public override string StateName => "RageHover";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.RageHover;

        private const int MaxVolleys = 3;

        private int VolleyInterval(PrimeStateContext ctx) => ctx.DeathMode ? 64 : 80;

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 2;

            Movement(context);
            LeanByVelocity(npc);

            //双子皆灭：机体过载漏血——狂暴阶段是有时限的搏命
            if (!VaultUtils.isClient && context.NoEye && npc.life > npc.lifeMax / 10) {
                npc.life -= 10;
            }

            //领域残留时只悬停压阵
            if (context.StormCount > 0) {
                return null;
            }

            Timer++;
            if (Timer >= VolleyInterval(context)) {
                Timer = 0;
                if (!VaultUtils.isClient) {
                    npc.TargetClosest();
                    FireVolley(context);
                    npc.netUpdate = true;
                }
                Counter++;
            }

            if (Counter >= MaxVolleys && !VaultUtils.isClient) {
                npc.TargetClosest();
                return ChooseNextAttack(context);
            }
            return null;
        }

        /// <summary>
        /// 固定出招序列：连冲 → 环形爆发 → 连冲 → 弹幕墙。
        /// 全难度共享同一套招式池，难度只影响数值密度
        /// </summary>
        private IPrimeState ChooseNextAttack(PrimeStateContext context) {
            int index = context.RageAttackIndex % 4;
            context.RageAttackIndex++;

            return index switch {
                0 => new PrimeRageDashState(),
                1 => new PrimeRadialBurstState(),
                2 => new PrimeRageDashState(),
                _ => new PrimeLaserWallState(),
            };
        }

        /// <summary>交替弹幕：偶数轮死亡激光扇面，奇数轮追踪火箭散射</summary>
        private void FireVolley(PrimeStateContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, ProjectileID.RocketSkeleton));

            if (Counter % 2 == 0) {
                int totalProjectiles = context.BossRush ? 9 : 6;
                if (!context.NoEye) {
                    totalProjectiles = 3;//双子还在时收敛火力，避免叠加压制过度
                }
                Vector2 fireDirection = npc.Center.To(target.Center).UnitVector();
                for (int j = 0; j < totalProjectiles; j++) {
                    Vector2 vector = fireDirection.RotatedBy((totalProjectiles / -2 + j) * 0.1f) * 6;
                    if (context.BossRush) {
                        vector *= 1.45f;
                    }
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + vector.UnitVector() * 100f,
                        vector, ModContent.ProjectileType<DeadLaser>(), damage, 0f, Main.myPlayer, 1f, 0f);
                }
                HeadPrimeAI.SpanFireLerterDustEffect(npc, 73);
            }
            else {
                //制导炮弹（带瞄准线预警）全难度统一使用，难度只影响弹数与张角
                int numProj = context.BossRush ? 5 : (context.DeathMode ? 4 : 3);
                float rotation = MathHelper.ToRadians(context.BossRush ? 15 : 9);
                Vector2 baseVelocity = (target.Center - npc.Center).SafeNormalize(Vector2.UnitY) * 10f;

                for (int i = 0; i < numProj; i++) {
                    float rotOffset = MathHelper.Lerp(-rotation, rotation, i / (float)(numProj - 1));
                    Vector2 perturbedSpeed = baseVelocity.RotatedBy(rotOffset);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, perturbedSpeed,
                        ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f,
                        Main.myPlayer, npc.whoAmI, npc.target, rotOffset);
                }
            }
        }

        private void Movement(PrimeStateContext context) {
            //失去四肢后机动性全面解放
            float vAccel = Main.masterMode ? 0.055f : 0.045f;
            float vMax = Main.masterMode ? 5.5f : 4.5f;
            float hAccel = Main.masterMode ? 0.13f : 0.11f;
            float hMax = Main.masterMode ? 11f : 10f;
            float decel = Main.masterMode ? 0.94f : 0.96f;
            if (context.DeathMode) {
                vAccel += 0.01f;
                vMax += 0.4f;
                hAccel += 0.05f;
                hMax += 1f;
            }
            if (context.BossRush) {
                vMax += 0.5f;
                hMax += 1f;
            }

            HoverMovement(context, vAccel, vMax, hAccel, hMax, decel, 150, 380);
        }
    }
}
