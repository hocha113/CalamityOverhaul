using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer
{
    /// <summary>
    /// 激光眼二阶段激光矩阵状态
    /// 在玩家周围布置激光发射点后同时发射
    /// </summary>
    internal class RetinazerLaserMatrixState : TwinsStateBase
    {
        public override string StateName => "RetinazerLaserMatrix";

        /// <summary>
        /// 定位阶段
        /// </summary>
        private const int PositionPhase = 35;

        /// <summary>
        /// 部署阶段
        /// </summary>
        private const int DeployPhase = 60;

        /// <summary>
        /// 蓄力阶段
        /// </summary>
        private const int ChargePhase = 45;

        /// <summary>
        /// 发射阶段
        /// </summary>
        private const int FirePhase = 20;

        /// <summary>
        /// 恢复阶段
        /// </summary>
        private const int RecoveryPhase = 25;

        /// <summary>
        /// 总时长
        /// </summary>
        private const int TotalDuration = PositionPhase + DeployPhase + ChargePhase + FirePhase + RecoveryPhase;

        /// <summary>
        /// 矩阵点数量
        /// </summary>
        private int MatrixPointCount => Context.IsMachineRebellion ? 6 : 4;

        private TwinsStateContext Context;
        private Vector2[] matrixPoints;
        private Vector2 centerPoint;
        private bool hasDeployed;
        private bool hasFired;

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            matrixPoints = new Vector2[MatrixPointCount];
            hasDeployed = false;
            hasFired = false;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //阶段1: 移动到位置
            if (Timer <= PositionPhase) {
                ExecutePositionPhase(npc, player);
            }
            //阶段2: 部署矩阵点
            else if (Timer <= PositionPhase + DeployPhase) {
                ExecuteDeployPhase(npc, player);
            }
            //阶段3: 蓄力
            else if (Timer <= PositionPhase + DeployPhase + ChargePhase) {
                ExecuteChargePhase(npc, player);
            }
            //阶段4: 发射
            else if (Timer <= PositionPhase + DeployPhase + ChargePhase + FirePhase) {
                ExecuteFirePhase(npc, player);
            }
            //阶段5: 恢复
            else {
                ExecuteRecoveryPhase(npc, player);
            }

            //状态结束
            if (Timer >= TotalDuration) {
                return new RetinazerHorizontalBarrageState();
            }

            return null;
        }

        /// <summary>
        /// 定位阶段
        /// </summary>
        private void ExecutePositionPhase(NPC npc, Player player) {
            //移动到玩家上方
            Vector2 targetPos = player.Center + new Vector2(0, -450);
            MoveTo(npc, targetPos, 16f, 0.12f);
            FaceTarget(npc, player.Center);

            //预警特效
            float progress = Timer / (float)PositionPhase;
            context.SetChargeState(7, progress * 0.2f);
        }

        /// <summary>
        /// 部署阶段
        /// </summary>
        private void ExecuteDeployPhase(NPC npc, Player player) {
            int phaseTimer = Timer - PositionPhase;
            float progress = phaseTimer / (float)DeployPhase;

            //记录中心点
            centerPoint = player.Center;

            //悬停在上方
            npc.velocity *= 0.92f;
            FaceTarget(npc, player.Center);

            //逐步部署矩阵点
            if (!hasDeployed) {
                for (int i = 0; i < MatrixPointCount; i++) {
                    float angle = MathHelper.TwoPi / MatrixPointCount * i + MathHelper.PiOver4;
                    float radius = 300f;
                    matrixPoints[i] = centerPoint + angle.ToRotationVector2() * radius;
                }
                hasDeployed = true;
            }

            //矩阵点渐显特效
            if (!VaultUtils.isServer) {
                int pointsToShow = (int)(progress * MatrixPointCount) + 1;
                pointsToShow = Math.Min(pointsToShow, MatrixPointCount);

                for (int i = 0; i < pointsToShow; i++) {
                    if (phaseTimer % 3 == 0) {
                        //矩阵点粒子
                        Vector2 pointPos = matrixPoints[i];
                        Dust dust = Dust.NewDustDirect(pointPos + Main.rand.NextVector2Circular(10, 10), 1, 1, DustID.Vortex, 0, 0, 100, default, 1.5f);
                        dust.noGravity = true;
                        dust.velocity = Vector2.Zero;

                        //连接线粒子
                        Vector2 toCenter = (centerPoint - pointPos).SafeNormalize(Vector2.Zero);
                        float lineDist = Vector2.Distance(pointPos, centerPoint) * progress;
                        Vector2 linePos = pointPos + toCenter * Main.rand.NextFloat(lineDist);
                        Dust lineDust = Dust.NewDustDirect(linePos, 1, 1, DustID.PurpleTorch, 0, 0, 150, default, 0.8f);
                        lineDust.noGravity = true;
                        lineDust.velocity = toCenter * 2f;
                    }
                }

                //部署音效
                if (phaseTimer == 1) {
                    SoundEngine.PlaySound(SoundID.Item94 with { Pitch = 0.3f, Volume = 0.7f }, npc.Center);
                }
            }

            //设置蓄力状态
            context.SetChargeState(7, 0.2f + progress * 0.3f);
        }

        /// <summary>
        /// 蓄力阶段
        /// </summary>
        private void ExecuteChargePhase(NPC npc, Player player) {
            int phaseTimer = Timer - PositionPhase - DeployPhase;
            float progress = phaseTimer / (float)ChargePhase;

            //悬停
            npc.velocity *= 0.95f;
            FaceTarget(npc, player.Center);

            //设置蓄力状态
            context.SetChargeState(7, 0.5f + progress * 0.5f);

            //所有矩阵点同时蓄力
            if (!VaultUtils.isServer) {
                for (int i = 0; i < MatrixPointCount; i++) {
                    Vector2 pointPos = matrixPoints[i];
                    Vector2 toCenter = (centerPoint - pointPos).SafeNormalize(Vector2.Zero);

                    //能量聚集
                    if (phaseTimer % 2 == 0) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = 40f - progress * 25f;
                        Vector2 dustPos = pointPos + angle.ToRotationVector2() * dist;
                        Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.Vortex, 0, 0, 100, default, 1.3f + progress);
                        dust.noGravity = true;
                        dust.velocity = (pointPos - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                    }

                    //瞄准线
                    if (phaseTimer % 4 == 0 && progress > 0.4f) {
                        float lineDist = 30f + (progress - 0.4f) / 0.6f * 200f;
                        Vector2 linePos = pointPos + toCenter * lineDist;
                        Dust dust = Dust.NewDustDirect(linePos, 1, 1, DustID.PurpleTorch, 0, 0, 100, default, 1.2f);
                        dust.noGravity = true;
                        dust.velocity = toCenter * 3f;
                    }
                }

                //蓄力完成闪光
                if (phaseTimer == ChargePhase - 3) {
                    for (int i = 0; i < MatrixPointCount; i++) {
                        Vector2 pointPos = matrixPoints[i];
                        for (int j = 0; j < 8; j++) {
                            float angle = MathHelper.TwoPi / 8f * j;
                            Vector2 vel = angle.ToRotationVector2() * 4f;
                            Dust dust = Dust.NewDustDirect(pointPos, 1, 1, DustID.Vortex, vel.X, vel.Y, 0, default, 1.8f);
                            dust.noGravity = true;
                        }
                    }
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f }, npc.Center);
                }
            }
        }

        /// <summary>
        /// 发射阶段
        /// </summary>
        private void ExecuteFirePhase(NPC npc, Player player) {
            int phaseTimer = Timer - PositionPhase - DeployPhase - ChargePhase;

            //停止蓄力特效
            context.ResetChargeState();

            //发射激光
            if (!hasFired) {
                hasFired = true;
                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = 0f, Volume = 1.3f }, npc.Center);

                if (!VaultUtils.isClient) {
                    for (int i = 0; i < MatrixPointCount; i++) {
                        Vector2 pointPos = matrixPoints[i];
                        Vector2 toCenter = (centerPoint - pointPos).SafeNormalize(Vector2.Zero);

                        //发射激光
                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            pointPos,
                            toCenter * 12f,
                            ProjectileID.DeathLaser,
                            35,
                            0f,
                            Main.myPlayer
                        );

                        //额外发射穿透激光
                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            pointPos,
                            toCenter * 8f,
                            ProjectileID.DeathLaser,
                            30,
                            0f,
                            Main.myPlayer
                        );
                    }
                }

                //发射特效
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < MatrixPointCount; i++) {
                        Vector2 pointPos = matrixPoints[i];
                        Vector2 toCenter = (centerPoint - pointPos).SafeNormalize(Vector2.Zero);

                        for (int j = 0; j < 10; j++) {
                            Vector2 dustVel = toCenter.RotatedBy((Main.rand.NextFloat() - 0.5f) * 0.4f) * Main.rand.NextFloat(6f, 12f);
                            Dust dust = Dust.NewDustDirect(pointPos, 1, 1, DustID.Vortex, dustVel.X, dustVel.Y, 0, default, 1.5f);
                            dust.noGravity = true;
                        }
                    }
                }
            }

            //后续的残留粒子
            if (!VaultUtils.isServer && phaseTimer % 3 == 0) {
                for (int i = 0; i < MatrixPointCount; i++) {
                    Vector2 pointPos = matrixPoints[i];
                    Dust dust = Dust.NewDustDirect(pointPos + Main.rand.NextVector2Circular(15, 15), 1, 1, DustID.PurpleTorch, 0, -2, 100, default, 0.9f);
                    dust.noGravity = true;
                }
            }
        }

        /// <summary>
        /// 恢复阶段
        /// </summary>
        private void ExecuteRecoveryPhase(NPC npc, Player player) {
            FaceTarget(npc, player.Center);
            npc.velocity *= 0.95f;

            //残余粒子
            if (!VaultUtils.isServer && Timer % 6 == 0) {
                Dust dust = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(25, 25), 1, 1, DustID.Vortex, 0, -1, 100, default, 0.7f);
                dust.noGravity = true;
            }
        }

        private TwinsStateContext context => Context;
    }
}
