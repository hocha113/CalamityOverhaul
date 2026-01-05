using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer
{
    /// <summary>
    /// 激光眼独眼狂暴状态
    /// 当魔焰眼死亡后进入，攻击更加疯狂和精准
    /// 包含多种激光攻击模式的快速切换
    /// </summary>
    internal class RetinazerSoloRageState : TwinsStateBase
    {
        public override string StateName => "RetinazerSoloRage";

        /// <summary>
        /// 当前攻击模式
        /// </summary>
        private enum RageAttackMode
        {
            /// <summary>
            /// 激光风暴 - 快速发射大量激光
            /// </summary>
            LaserStorm,
            /// <summary>
            /// 交叉射线 - 从多个方向发射交叉激光
            /// </summary>
            CrossBeams,
            /// <summary>
            /// 追踪激光 - 持续追踪玩家发射激光
            /// </summary>
            HomingLaser,
            /// <summary>
            /// 激光矩阵 - 在玩家周围布置激光阵列
            /// </summary>
            LaserMatrix
        }

        private TwinsStateContext Context;
        private RageAttackMode currentMode;
        private int modeTimer;
        private int attackCount;
        private int totalAttacks;
        private Vector2[] matrixPoints;
        private float sweepAngle;
        private bool hasPlayedModeSound;

        //难度调整参数
        private int LaserStormFireRate => Context.IsMachineRebellion ? 3 : (Context.IsDeathMode ? 4 : 5);
        private int LaserStormDuration => Context.IsMachineRebellion ? 120 : (Context.IsDeathMode ? 100 : 80);
        private float LaserSpeed => Context.IsMachineRebellion ? 18f : (Context.IsDeathMode ? 16f : 14f);
        private int CrossBeamCount => Context.IsMachineRebellion ? 8 : (Context.IsDeathMode ? 6 : 5);
        private int MatrixPointCount => Context.IsMachineRebellion ? 8 : (Context.IsDeathMode ? 6 : 5);

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            currentMode = RageAttackMode.LaserStorm;
            modeTimer = 0;
            attackCount = 0;
            totalAttacks = 0;
            sweepAngle = 0f;
            hasPlayedModeSound = false;
            matrixPoints = new Vector2[MatrixPointCount];

            //清除狂暴触发标记
            context.SoloRageJustTriggered = false;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            modeTimer++;

            //根据当前模式执行不同的攻击
            switch (currentMode) {
                case RageAttackMode.LaserStorm:
                    ExecuteLaserStorm(npc, player);
                    break;
                case RageAttackMode.CrossBeams:
                    ExecuteCrossBeams(npc, player);
                    break;
                case RageAttackMode.HomingLaser:
                    ExecuteHomingLaser(npc, player);
                    break;
                case RageAttackMode.LaserMatrix:
                    ExecuteLaserMatrix(npc, player);
                    break;
            }

            //持续产生狂暴粒子效果
            if (!VaultUtils.isServer && Timer % 3 == 0) {
                Dust dust = Dust.NewDustDirect(
                    npc.Center + Main.rand.NextVector2Circular(30, 30),
                    1, 1, DustID.Vortex, 0, 0, 100, default, 1.5f);
                dust.noGravity = true;
                dust.velocity = Main.rand.NextVector2Circular(3, 3);
            }

            //独眼狂暴模式不会自动切换出去
            return null;
        }

        /// <summary>
        /// 切换到下一个攻击模式
        /// </summary>
        private void SwitchToNextMode() {
            totalAttacks++;
            modeTimer = 0;
            attackCount = 0;
            hasPlayedModeSound = false;
            sweepAngle = 0f;

            //随机选择下一个模式
            RageAttackMode previousMode = currentMode;
            do {
                currentMode = (RageAttackMode)Main.rand.Next(4);
            } while (currentMode == previousMode && Main.rand.NextFloat() < 0.7f);

            //重新初始化矩阵点
            if (currentMode == RageAttackMode.LaserMatrix) {
                matrixPoints = new Vector2[MatrixPointCount];
            }
        }

        /// <summary>
        /// 激光风暴模式 - 快速发射大量激光
        /// </summary>
        private void ExecuteLaserStorm(NPC npc, Player player) {
            if (!hasPlayedModeSound) {
                hasPlayedModeSound = true;
                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = 0.3f, Volume = 1.2f }, npc.Center);
            }

            //快速移动保持在玩家侧面
            Vector2 hoverPos = player.Center + new Vector2(npc.Center.X < player.Center.X ? -350 : 350, -150);
            MoveTo(npc, hoverPos, 14f, 0.1f);
            FaceTarget(npc, player.Center);

            //快速发射激光
            if (modeTimer % LaserStormFireRate == 0 && !VaultUtils.isClient) {
                Vector2 toPlayer = GetDirectionToTarget(Context);
                //添加轻微散射
                float scatter = (Main.rand.NextFloat() - 0.5f) * 0.15f;
                Vector2 shootDir = toPlayer.RotatedBy(scatter);

                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    shootDir * LaserSpeed,
                    ProjectileID.DeathLaser,
                    30,
                    0f,
                    Main.myPlayer
                );

                //每隔几发发射一个强力激光
                if (modeTimer % (LaserStormFireRate * 4) == 0) {
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        toPlayer * (LaserSpeed * 0.8f),
                        ModContent.ProjectileType<DeadLaser>(),
                        45,
                        0f,
                        Main.myPlayer
                    );
                }

                SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.2f, Volume = 0.6f }, npc.Center);
            }

            //发射粒子
            if (!VaultUtils.isServer && modeTimer % 2 == 0) {
                Vector2 toPlayer = GetDirectionToTarget(Context);
                Dust dust = Dust.NewDustDirect(npc.Center + toPlayer * 35f, 1, 1, DustID.Vortex,
                    toPlayer.X * 5, toPlayer.Y * 5, 0, default, 1.2f);
                dust.noGravity = true;
            }

            if (modeTimer >= LaserStormDuration) {
                SwitchToNextMode();
            }
        }

        /// <summary>
        /// 交叉射线模式 - 从多个角度发射交叉激光
        /// </summary>
        private void ExecuteCrossBeams(NPC npc, Player player) {
            int chargeTime = 50;
            int fireTime = 30;
            int totalTime = chargeTime + fireTime;

            if (!hasPlayedModeSound) {
                hasPlayedModeSound = true;
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.2f, Volume = 0.9f }, npc.Center);
            }

            //悬停在玩家上方
            Vector2 hoverPos = player.Center + new Vector2(0, -400);
            MoveTo(npc, hoverPos, 10f, 0.08f);
            FaceTarget(npc, player.Center);

            //蓄力阶段
            if (modeTimer < chargeTime) {
                float progress = modeTimer / (float)chargeTime;
                Context.SetChargeState(6, progress);

                //蓄力粒子
                if (!VaultUtils.isServer && modeTimer % 2 == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 100f - progress * 60f;
                    Vector2 dustPos = npc.Center + angle.ToRotationVector2() * dist;
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.Vortex, 0, 0, 100, default, 1.6f + progress);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                }

                //预警线
                if (!VaultUtils.isServer && modeTimer % 4 == 0 && progress > 0.5f) {
                    Vector2 toPlayer = GetDirectionToTarget(Context);
                    for (int i = 0; i < CrossBeamCount; i++) {
                        float beamAngle = MathHelper.TwoPi / CrossBeamCount * i;
                        Vector2 beamDir = toPlayer.RotatedBy(beamAngle);
                        float lineDist = 50f + (progress - 0.5f) * 200f;
                        Vector2 linePos = npc.Center + beamDir * lineDist;
                        Dust dust = Dust.NewDustDirect(linePos, 1, 1, DustID.PurpleTorch, 0, 0, 150, default, 1.3f);
                        dust.noGravity = true;
                        dust.velocity = beamDir * 2f;
                    }
                }
            }
            //发射阶段
            else if (modeTimer == chargeTime) {
                Context.ResetChargeState();
                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = -0.1f, Volume = 1.3f }, npc.Center);

                if (!VaultUtils.isClient) {
                    Vector2 toPlayer = GetDirectionToTarget(Context);
                    for (int i = 0; i < CrossBeamCount; i++) {
                        float beamAngle = MathHelper.TwoPi / CrossBeamCount * i;
                        Vector2 beamDir = toPlayer.RotatedBy(beamAngle);

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            beamDir * LaserSpeed,
                            ModContent.ProjectileType<DeadLaser>(),
                            40,
                            0f,
                            Main.myPlayer
                        );
                    }
                }

                //发射特效
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 20; i++) {
                        float angle = MathHelper.TwoPi / 20f * i;
                        Vector2 vel = angle.ToRotationVector2() * 8f;
                        Dust dust = Dust.NewDustDirect(npc.Center, 1, 1, DustID.Vortex, vel.X, vel.Y, 0, default, 2f);
                        dust.noGravity = true;
                    }
                }

                //后坐力
                npc.velocity = -GetDirectionToTarget(Context) * 10f;
                attackCount++;
            }

            if (modeTimer >= totalTime) {
                if (attackCount >= 3) {
                    SwitchToNextMode();
                }
                else {
                    modeTimer = 0;
                    hasPlayedModeSound = false;
                }
            }
        }

        /// <summary>
        /// 追踪激光模式 - 持续追踪玩家发射激光
        /// </summary>
        private void ExecuteHomingLaser(NPC npc, Player player) {
            int homingDuration = Context.IsMachineRebellion ? 150 : (Context.IsDeathMode ? 120 : 100);

            if (!hasPlayedModeSound) {
                hasPlayedModeSound = true;
                SoundEngine.PlaySound(SoundID.Item12 with { Pitch = -0.2f, Volume = 1.1f }, npc.Center);
            }

            //围绕玩家移动，保持一定距离
            sweepAngle += Context.IsDeathMode ? 0.04f : 0.03f;
            float radius = 350f + (float)Math.Sin(modeTimer * 0.03f) * 50f;
            Vector2 targetPos = player.Center + sweepAngle.ToRotationVector2() * radius;

            MoveTo(npc, targetPos, 16f, 0.12f);
            FaceTarget(npc, player.Center);

            //持续发射激光
            int fireRate = Context.IsMachineRebellion ? 8 : (Context.IsDeathMode ? 10 : 12);
            if (modeTimer % fireRate == 0 && !VaultUtils.isClient) {
                Vector2 toPlayer = GetDirectionToTarget(Context);

                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    toPlayer * LaserSpeed,
                    ProjectileID.DeathLaser,
                    28,
                    0f,
                    Main.myPlayer
                );

                SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.1f, Volume = 0.7f }, npc.Center);
            }

            //间歇性发射强力激光
            if (modeTimer % 30 == 0 && !VaultUtils.isClient) {
                Vector2 toPlayer = GetDirectionToTarget(Context);
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    toPlayer * (LaserSpeed * 0.8f),
                    ModContent.ProjectileType<DeadLaser>(),
                    42,
                    0f,
                    Main.myPlayer
                );
            }

            //轨迹粒子
            if (!VaultUtils.isServer && modeTimer % 2 == 0) {
                Vector2 tangent = (sweepAngle + MathHelper.PiOver2).ToRotationVector2();
                Dust dust = Dust.NewDustDirect(npc.Center, 1, 1, DustID.Vortex,
                    tangent.X * 2, tangent.Y * 2, 100, default, 1.3f);
                dust.noGravity = true;
            }

            if (modeTimer >= homingDuration) {
                SwitchToNextMode();
            }
        }

        /// <summary>
        /// 激光矩阵模式 - 在玩家周围布置激光阵列
        /// </summary>
        private void ExecuteLaserMatrix(NPC npc, Player player) {
            int deployTime = 60;
            int chargeTime = 40;
            int fireTime = 20;
            int totalTime = deployTime + chargeTime + fireTime;

            if (!hasPlayedModeSound) {
                hasPlayedModeSound = true;
                SoundEngine.PlaySound(SoundID.Item94 with { Pitch = 0.3f, Volume = 0.9f }, npc.Center);
            }

            //悬停在玩家上方
            Vector2 hoverPos = player.Center + new Vector2(0, -450);
            MoveTo(npc, hoverPos, 12f, 0.1f);
            FaceTarget(npc, player.Center);

            //部署阶段
            if (modeTimer < deployTime) {
                float progress = modeTimer / (float)deployTime;

                //计算矩阵点位置
                for (int i = 0; i < MatrixPointCount; i++) {
                    float angle = MathHelper.TwoPi / MatrixPointCount * i + MathHelper.PiOver4;
                    float matrixRadius = 320f;
                    matrixPoints[i] = player.Center + angle.ToRotationVector2() * matrixRadius;
                }

                //矩阵点渐显特效
                if (!VaultUtils.isServer) {
                    int pointsToShow = (int)(progress * MatrixPointCount) + 1;
                    pointsToShow = Math.Min(pointsToShow, MatrixPointCount);

                    for (int i = 0; i < pointsToShow; i++) {
                        if (modeTimer % 3 == 0) {
                            Vector2 pointPos = matrixPoints[i];
                            Dust dust = Dust.NewDustDirect(pointPos + Main.rand.NextVector2Circular(15, 15), 1, 1, DustID.Vortex, 0, 0, 100, default, 1.4f);
                            dust.noGravity = true;
                            dust.velocity = Vector2.Zero;
                        }
                    }
                }

                Context.SetChargeState(7, progress * 0.4f);
            }
            //蓄力阶段
            else if (modeTimer < deployTime + chargeTime) {
                int phaseTimer = modeTimer - deployTime;
                float progress = phaseTimer / (float)chargeTime;

                Context.SetChargeState(7, 0.4f + progress * 0.6f);

                //所有矩阵点蓄力特效
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < MatrixPointCount; i++) {
                        Vector2 pointPos = matrixPoints[i];
                        Vector2 toCenter = (player.Center - pointPos).SafeNormalize(Vector2.Zero);

                        //能量聚集
                        if (phaseTimer % 2 == 0) {
                            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                            float dist = 40f - progress * 25f;
                            Vector2 dustPos = pointPos + angle.ToRotationVector2() * dist;
                            Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.Vortex, 0, 0, 100, default, 1.3f);
                            dust.noGravity = true;
                            dust.velocity = (pointPos - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                        }

                        //瞄准线
                        if (phaseTimer % 4 == 0 && progress > 0.3f) {
                            float lineDist = 30f + progress * 150f;
                            Vector2 linePos = pointPos + toCenter * lineDist;
                            Dust dust = Dust.NewDustDirect(linePos, 1, 1, DustID.PurpleTorch, 0, 0, 100, default, 1.1f);
                            dust.noGravity = true;
                            dust.velocity = toCenter * 3f;
                        }
                    }

                    //蓄力完成闪光
                    if (phaseTimer == chargeTime - 3) {
                        for (int i = 0; i < MatrixPointCount; i++) {
                            Vector2 pointPos = matrixPoints[i];
                            for (int j = 0; j < 8; j++) {
                                float angle = MathHelper.TwoPi / 8f * j;
                                Vector2 vel = angle.ToRotationVector2() * 4f;
                                Dust dust = Dust.NewDustDirect(pointPos, 1, 1, DustID.Vortex, vel.X, vel.Y, 0, default, 1.6f);
                                dust.noGravity = true;
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f }, npc.Center);
                    }
                }
            }
            //发射阶段
            else if (modeTimer == deployTime + chargeTime) {
                Context.ResetChargeState();
                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = 0f, Volume = 1.4f }, npc.Center);

                if (!VaultUtils.isClient) {
                    for (int i = 0; i < MatrixPointCount; i++) {
                        Vector2 pointPos = matrixPoints[i];
                        Vector2 toCenter = (player.Center - pointPos).SafeNormalize(Vector2.Zero);

                        //发射多发激光
                        for (int j = 0; j < 2; j++) {
                            float speedMult = 1f - j * 0.3f;
                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                pointPos,
                                toCenter * (LaserSpeed * speedMult),
                                j == 0 ? ModContent.ProjectileType<DeadLaser>() : ProjectileID.DeathLaser,
                                j == 0 ? 40 : 32,
                                0f,
                                Main.myPlayer
                            );
                        }
                    }
                }

                //发射特效
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < MatrixPointCount; i++) {
                        Vector2 pointPos = matrixPoints[i];
                        Vector2 toCenter = (player.Center - pointPos).SafeNormalize(Vector2.Zero);
                        for (int j = 0; j < 8; j++) {
                            Vector2 dustVel = toCenter.RotatedBy((Main.rand.NextFloat() - 0.5f) * 0.4f) * Main.rand.NextFloat(6f, 10f);
                            Dust dust = Dust.NewDustDirect(pointPos, 1, 1, DustID.Vortex, dustVel.X, dustVel.Y, 0, default, 1.4f);
                            dust.noGravity = true;
                        }
                    }
                }

                attackCount++;
            }

            if (modeTimer >= totalTime) {
                if (attackCount >= 2) {
                    SwitchToNextMode();
                }
                else {
                    modeTimer = 0;
                    hasPlayedModeSound = false;
                }
            }
        }

        private TwinsStateContext context => Context;
    }
}
