using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer
{
    /// <summary>激光矩阵，玩家周围布点→蓄力→齐射</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.RetinazerLaserMatrix, typeof(TwinsStateContext))]
    internal class RetinazerLaserMatrixState : TwinsStateBase
    {
        public override string StateName => "RetinazerLaserMatrix";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.RetinazerLaserMatrix;

        private int PositionPhase => Context.IsAsuraMode ? 28 : 35;

        private int DeployPhase => Context.IsAsuraMode ? 50 : 60;

        private int ChargePhase => Context.IsAsuraMode ? 38 : 45;

        private int FirePhase => Context.IsAsuraMode ? 18 : 20;

        private int RecoveryPhase => Context.IsAsuraMode ? 20 : 25;

        private int TotalDuration => PositionPhase + DeployPhase + ChargePhase + FirePhase + RecoveryPhase;

        private int MatrixPointCount => Context.IsAsuraMode ? 5 : 4;

        private float LaserSpeed => Context.IsAsuraMode ? 14f : 12f;

        private TwinsStateContext Context;
        private Vector2[] matrixPoints;
        private Vector2 centerPoint;
        private bool hasDeployed;
        private bool hasFired;
        private int comboStep;

        public RetinazerLaserMatrixState() : this(0) {
        }

        public RetinazerLaserMatrixState(int currentComboStep) {
            comboStep = currentComboStep;
        }

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

            if (Timer <= PositionPhase) {
                ExecutePositionPhase(npc, player);
            }
            else if (Timer <= PositionPhase + DeployPhase) {
                ExecuteDeployPhase(npc, player);
            }
            else if (Timer <= PositionPhase + DeployPhase + ChargePhase) {
                ExecuteChargePhase(npc, player);
            }
            else if (Timer <= PositionPhase + DeployPhase + ChargePhase + FirePhase) {
                ExecuteFirePhase(npc, player);
            }
            else {
                ExecuteRecoveryPhase(npc, player);
            }

            if (Timer >= TotalDuration) {
                if (context.IsSoloRageMode) {
                    return new RetinazerSoloRageState();
                }
                return new RetinazerVerticalBarrageState(comboStep);
            }

            return null;
        }

        private void ExecutePositionPhase(NPC npc, Player player) {
            Vector2 targetPos = player.Center + new Vector2(0, -450);
            MoveTo(npc, targetPos, 16f, 0.12f);
            FaceTarget(npc, player.Center);

            float progress = Timer / (float)PositionPhase;
            context.SetChargeState(7, progress * 0.2f);
        }

        private void ExecuteDeployPhase(NPC npc, Player player) {
            int phaseTimer = Timer - PositionPhase;
            float progress = phaseTimer / (float)DeployPhase;

            centerPoint = player.Center;

            npc.velocity *= 0.92f;
            FaceTarget(npc, player.Center);

            if (!hasDeployed) {
                for (int i = 0; i < MatrixPointCount; i++) {
                    float angle = MathHelper.TwoPi / MatrixPointCount * i + MathHelper.PiOver4;
                    float radius = 300f;
                    matrixPoints[i] = centerPoint + angle.ToRotationVector2() * radius;
                }
                hasDeployed = true;
            }

            if (!VaultUtils.isServer) {
                int pointsToShow = (int)(progress * MatrixPointCount) + 1;
                pointsToShow = Math.Min(pointsToShow, MatrixPointCount);

                for (int i = 0; i < pointsToShow; i++) {
                    //节点显形涟漪
                    if (phaseTimer == (int)(i * DeployPhase / (float)MatrixPointCount) + 1) {
                        PRTLoader.NewParticle<PRT_DWave>(matrixPoints[i], Vector2.Zero, TwinsMotion.RetinColor, 0.1f)?
                            .Configure(Vector2.One, 0f, 0.55f, 14);
                        SoundEngine.PlaySound(SoundID.Item94 with { Pitch = 0.3f + i * 0.08f, Volume = 0.6f }, matrixPoints[i]);
                    }

                    if (phaseTimer % 3 == 0) {
                        //矩阵节点能量标记
                        Vector2 pointPos = matrixPoints[i];
                        PRTLoader.NewParticle<PRT_TwinsSpark>(pointPos + Main.rand.NextVector2Circular(12, 12),
                            Main.rand.NextVector2Circular(1f, 1f), Color.White, Main.rand.NextFloat(1f, 1.5f))?.Configure(14, 0);

                        //连接线粒子
                        Vector2 toCenter = (centerPoint - pointPos).SafeNormalize(Vector2.Zero);
                        float lineDist = Vector2.Distance(pointPos, centerPoint) * progress;
                        Vector2 linePos = pointPos + toCenter * Main.rand.NextFloat(lineDist);
                        Dust lineDust = Dust.NewDustDirect(linePos, 1, 1, DustID.PurpleTorch, 0, 0, 150, default, 0.8f);
                        lineDust.noGravity = true;
                        lineDust.velocity = toCenter * 2f;
                    }
                }
            }

            context.SetChargeState(7, 0.2f + progress * 0.3f);
        }

        private void ExecuteChargePhase(NPC npc, Player player) {
            int phaseTimer = Timer - PositionPhase - DeployPhase;
            float progress = phaseTimer / (float)ChargePhase;

            npc.velocity *= 0.95f;
            FaceTarget(npc, player.Center);

            context.SetChargeState(7, 0.5f + progress * 0.5f);

            if (!VaultUtils.isServer) {
                for (int i = 0; i < MatrixPointCount; i++) {
                    Vector2 pointPos = matrixPoints[i];
                    Vector2 toCenter = (centerPoint - pointPos).SafeNormalize(Vector2.Zero);

                    if (phaseTimer % 2 == 0) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = 40f - progress * 25f;
                        Vector2 dustPos = pointPos + angle.ToRotationVector2() * dist;
                        Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.Vortex, 0, 0, 100, default, 1.3f + progress);
                        dust.noGravity = true;
                        dust.velocity = (pointPos - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                    }

                    if (phaseTimer % 4 == 0 && progress > 0.4f) {
                        float lineDist = 30f + (progress - 0.4f) / 0.6f * 200f;
                        Vector2 linePos = pointPos + toCenter * lineDist;
                        Dust dust = Dust.NewDustDirect(linePos, 1, 1, DustID.PurpleTorch, 0, 0, 100, default, 1.2f);
                        dust.noGravity = true;
                        dust.velocity = toCenter * 3f;
                    }
                }

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

        private void ExecuteFirePhase(NPC npc, Player player) {
            int phaseTimer = Timer - PositionPhase - DeployPhase - ChargePhase;

            context.ResetChargeState();

            if (!hasFired) {
                hasFired = true;
                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = 0f, Volume = 1.3f }, npc.Center);

                if (!VaultUtils.isClient) {
                    for (int i = 0; i < MatrixPointCount; i++) {
                        Vector2 pointPos = matrixPoints[i];
                        Vector2 toCenter = (centerPoint - pointPos).SafeNormalize(Vector2.Zero);

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            pointPos,
                            toCenter * LaserSpeed,
                            ModContent.ProjectileType<RetinazerLaser>(),
                            28,
                            0f,
                            Main.myPlayer
                        );

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            pointPos,
                            toCenter * (LaserSpeed * 0.7f),
                            ModContent.ProjectileType<RetinazerLaser>(),
                            24,
                            0f,
                            Main.myPlayer
                        );
                    }
                }

                //节点发射火花
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < MatrixPointCount; i++) {
                        Vector2 pointPos = matrixPoints[i];
                        Vector2 toCenter = (centerPoint - pointPos).SafeNormalize(Vector2.Zero);

                        PRTLoader.NewParticle<PRT_DWave>(pointPos, Vector2.Zero, TwinsMotion.RetinColor, 0.12f)?
                            .Configure(new Vector2(1.3f, 0.7f), toCenter.ToRotation() + MathHelper.PiOver2, 0.6f, 12);
                        for (int j = 0; j < 7; j++) {
                            PRTLoader.NewParticle<PRT_TwinsSpark>(pointPos,
                                toCenter.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(5f, 10f),
                                Color.White, Main.rand.NextFloat(1f, 1.6f))?.Configure(16, 0);
                        }
                    }
                    TwinsMotion.Shake(centerPoint, 4f, 10);
                }
            }

            if (!VaultUtils.isServer && phaseTimer % 3 == 0) {
                for (int i = 0; i < MatrixPointCount; i++) {
                    Vector2 pointPos = matrixPoints[i];
                    Dust dust = Dust.NewDustDirect(pointPos + Main.rand.NextVector2Circular(15, 15), 1, 1, DustID.PurpleTorch, 0, -2, 100, default, 0.9f);
                    dust.noGravity = true;
                }
            }
        }

        private void ExecuteRecoveryPhase(NPC npc, Player player) {
            FaceTarget(npc, player.Center);
            npc.velocity *= 0.95f;

            if (!VaultUtils.isServer && Timer % 6 == 0) {
                Dust dust = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(25, 25), 1, 1, DustID.Vortex, 0, -1, 100, default, 0.7f);
                dust.noGravity = true;
            }
        }

        private TwinsStateContext context => Context;
    }
}
