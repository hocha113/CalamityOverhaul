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
    /// <summary>激光眼独眼狂暴，魔焰眼死后切入，四模式快切循环</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.RetinazerSoloRage, typeof(TwinsStateContext))]
    internal class RetinazerSoloRageState : TwinsStateBase
    {
        public override string StateName => "RetinazerSoloRage";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.RetinazerSoloRage;

        private enum RageAttackMode
        {
            /// <summary>激光风暴</summary>
            LaserStorm,
            /// <summary>交叉射线</summary>
            CrossBeams,
            /// <summary>追踪激光</summary>
            HomingLaser,
            /// <summary>激光矩阵</summary>
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

        /// <summary>换招连接节拍剩余帧，让段落间隔被看见</summary>
        private int modeTransitionTimer;

        private int ModeTransitionTime => Context.IsDeathMode ? 14 : 18;

        private int LaserStormFireRate => Context.IsDeathMode ? 6 : 8;
        private int LaserStormDuration => Context.IsDeathMode ? 90 : 75;
        private float LaserSpeed => Context.IsDeathMode ? 16f : 14f;
        private int CrossBeamCount => Context.IsDeathMode ? 5 : 4;
        private int MatrixPointCount => Context.IsDeathMode ? 5 : 4;

        public RetinazerSoloRageState() {
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            currentMode = RageAttackMode.LaserStorm;
            modeTimer = 0;
            attackCount = 0;
            totalAttacks = 0;
            sweepAngle = 0f;
            hasPlayedModeSound = false;
            modeTransitionTimer = 0;
            matrixPoints = new Vector2[MatrixPointCount];

            context.SoloRageJustTriggered = false;

            //狂暴觉醒倒灌
            if (!VaultUtils.isServer) {
                NPC npc = context.Npc;
                for (int i = 0; i < 26; i++) {
                    Vector2 spawnPos = npc.Center + Main.rand.NextVector2CircularEdge(220f, 220f);
                    PRTLoader.NewParticle<PRT_TwinsSpark>(spawnPos, (npc.Center - spawnPos) * 0.07f,
                        Color.White, Main.rand.NextFloat(1.2f, 2f))?.Configure(28, 0);
                }
                PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero, TwinsMotion.RetinColor, 1.1f)?
                    .Configure(Vector2.One, 0f, 0.15f, 22);
                TwinsMotion.Shake(npc.Center, 7f, 16);
            }
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //换招连接节拍，减速对视再起手
            if (modeTransitionTimer > 0) {
                modeTransitionTimer--;
                npc.velocity *= 0.88f;
                FaceTarget(npc, player.Center);
                Context.ResetChargeState();

                if (!VaultUtils.isServer && modeTransitionTimer % 4 == 0) {
                    PRTLoader.NewParticle<PRT_Smoke>(npc.Center + Main.rand.NextVector2Circular(22, 22),
                        new Vector2(0, -1.6f) + Main.rand.NextVector2Circular(0.7f, 0.7f),
                        TwinsMotion.RetinColor * 0.5f, Main.rand.NextFloat(0.6f, 1f))?
                        .Configure(30, 0.5f, 0.02f, false, 0f);
                }
                return null;
            }

            modeTimer++;

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

            //狂暴余温火花
            if (!VaultUtils.isServer && Timer % 3 == 0) {
                PRTLoader.NewParticle<PRT_TwinsSpark>(
                    npc.Center + Main.rand.NextVector2Circular(30, 30),
                    Main.rand.NextVector2Circular(2.5f, 2.5f) - new Vector2(0, 1.2f),
                    Color.White, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(16, 0);
            }

            //独眼狂暴模式不会自动切换出去
            return null;
        }

        /// <summary>狂暴循环，风暴→交叉→追踪→矩阵</summary>
        private static readonly RageAttackMode[] RageComboSequence =
        [
            RageAttackMode.LaserStorm,
            RageAttackMode.CrossBeams,
            RageAttackMode.HomingLaser,
            RageAttackMode.LaserMatrix
        ];

        private void SwitchToNextMode() {
            totalAttacks++;
            modeTimer = 0;
            attackCount = 0;
            hasPlayedModeSound = false;
            sweepAngle = 0f;
            modeTransitionTimer = ModeTransitionTime;

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.35f, Volume = 0.9f }, Context.Npc.Center);
            }

            currentMode = RageComboSequence[totalAttacks % RageComboSequence.Length];

            //重新初始化矩阵点
            if (currentMode == RageAttackMode.LaserMatrix) {
                matrixPoints = new Vector2[MatrixPointCount];
            }
        }

        /// <summary>激光风暴，快射大量激光</summary>
        private void ExecuteLaserStorm(NPC npc, Player player) {
            if (!hasPlayedModeSound) {
                hasPlayedModeSound = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item33 with { Pitch = 0.3f, Volume = 1.2f }, npc.Center);
                }
            }

            //弹簧游走保持在玩家侧面，密集连射需要足够的飞行距离
            Vector2 hoverPos = player.Center
                + new Vector2(npc.Center.X < player.Center.X ? -420 : 420, -190)
                + TwinsMotion.BreathingOffset(seed: 5.3f, 12f);
            TwinsMotion.SpringHover(npc, hoverPos, 0.016f, 0.09f);
            FaceTarget(npc, player.Center);

            //快速发射激光
            if (modeTimer % LaserStormFireRate == 0) {
                Vector2 toPlayer = GetDirectionToTarget(Context);
                if (!VaultUtils.isClient) {
                    //基于计时器的确定性散射
                    int shotIndex = modeTimer / LaserStormFireRate;
                    float scatter = MathHelper.Lerp(-0.075f, 0.075f, (shotIndex % 10) / 9f);
                    Vector2 shootDir = toPlayer.RotatedBy(scatter);

                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center + shootDir * 38f,
                        shootDir * LaserSpeed,
                        ModContent.ProjectileType<RetinazerLaser>(),
                        22,
                        0f,
                        Main.myPlayer
                    );

                    //每隔几发发射一个强化激光
                    if (modeTimer % (LaserStormFireRate * 4) == 0) {
                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center + toPlayer * 38f,
                            toPlayer * (LaserSpeed * 0.8f),
                            ModContent.ProjectileType<RetinazerLaser>(),
                            34,
                            0f,
                            Main.myPlayer,
                            0f,
                            1f
                        );
                    }
                }

                //每发后坐力
                npc.velocity -= toPlayer * 2.8f;

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.2f, Volume = 0.6f }, npc.Center);
                    for (int i = 0; i < 2; i++) {
                        PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center + toPlayer * 40f,
                            toPlayer.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f)) * Main.rand.NextFloat(3f, 5f),
                            Color.White, Main.rand.NextFloat(0.8f, 1.2f))?.Configure(12, 0);
                    }
                }
            }

            if (modeTimer >= LaserStormDuration) {
                SwitchToNextMode();
            }
        }

        /// <summary>交叉射线，多角度交叉激光</summary>
        private void ExecuteCrossBeams(NPC npc, Player player) {
            int chargeTime = 50;
            int fireTime = 30;
            int totalTime = chargeTime + fireTime;

            if (!hasPlayedModeSound) {
                hasPlayedModeSound = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.2f, Volume = 0.9f }, npc.Center);
                }
            }

            //悬停在玩家上方
            Vector2 hoverPos = player.Center + new Vector2(0, -400);
            MoveTo(npc, hoverPos, 10f, 0.08f);
            FaceTarget(npc, player.Center);

            if (modeTimer < chargeTime) {
                float progress = modeTimer / (float)chargeTime;
                Context.SetChargeState(6, progress);

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
            else if (modeTimer == chargeTime) {
                Context.ResetChargeState();
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item33 with { Pitch = -0.1f, Volume = 1.3f }, npc.Center);
                }

                if (!VaultUtils.isClient) {
                    Vector2 toPlayer = GetDirectionToTarget(Context);
                    for (int i = 0; i < CrossBeamCount; i++) {
                        float beamAngle = MathHelper.TwoPi / CrossBeamCount * i;
                        Vector2 beamDir = toPlayer.RotatedBy(beamAngle);

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            beamDir * LaserSpeed,
                            ModContent.ProjectileType<RetinazerLaser>(),
                            32,
                            0f,
                            Main.myPlayer,
                            0f,
                            1f
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

        /// <summary>追踪激光，持续跟踪射击</summary>
        private void ExecuteHomingLaser(NPC npc, Player player) {
            int homingDuration = Context.IsDeathMode ? 120 : 100;

            if (!hasPlayedModeSound) {
                hasPlayedModeSound = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item12 with { Pitch = -0.2f, Volume = 1.1f }, npc.Center);
                }
            }

            //围绕玩家移动，保持一定距离
            sweepAngle += Context.IsDeathMode ? 0.04f : 0.03f;
            float radius = 350f + (float)Math.Sin(modeTimer * 0.03f) * 50f;
            Vector2 targetPos = player.Center + sweepAngle.ToRotationVector2() * radius;

            MoveTo(npc, targetPos, 16f, 0.12f);
            FaceTarget(npc, player.Center);

            //持续发射预判激光
            int fireRate = Context.IsDeathMode ? 13 : 15;
            if (modeTimer % fireRate == 0) {
                Vector2 predicted = TwinsMotion.PredictTarget(player, npc.Center, LaserSpeed * 3f, 0.45f);
                Vector2 shootDir = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center + shootDir * 38f,
                        shootDir * LaserSpeed,
                        ModContent.ProjectileType<RetinazerLaser>(),
                        22,
                        0f,
                        Main.myPlayer
                    );
                }
                npc.velocity -= shootDir * 2.2f;

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.1f, Volume = 0.7f }, npc.Center);
                }
            }

            //间歇性发射强化激光
            if (modeTimer % 35 == 0 && !VaultUtils.isClient) {
                Vector2 toPlayer = GetDirectionToTarget(Context);
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center + toPlayer * 38f,
                    toPlayer * (LaserSpeed * 0.8f),
                    ModContent.ProjectileType<RetinazerLaser>(),
                    34,
                    0f,
                    Main.myPlayer,
                    0f,
                    1f
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

        /// <summary>激光矩阵，环绕布点齐射</summary>
        private void ExecuteLaserMatrix(NPC npc, Player player) {
            int deployTime = 60;
            int chargeTime = 40;
            int fireTime = 20;
            int totalTime = deployTime + chargeTime + fireTime;

            if (!hasPlayedModeSound) {
                hasPlayedModeSound = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item94 with { Pitch = 0.3f, Volume = 0.9f }, npc.Center);
                }
            }

            //悬停在玩家上方
            Vector2 hoverPos = player.Center + new Vector2(0, -450);
            MoveTo(npc, hoverPos, 12f, 0.1f);
            FaceTarget(npc, player.Center);

            if (modeTimer < deployTime) {
                float progress = modeTimer / (float)deployTime;
                float value = Context.IsDeathMode ? 0.8f : 0.65f;
                if (progress < value) {
                    //计算矩阵点位置
                    for (int i = 0; i < MatrixPointCount; i++) {
                        float angle = MathHelper.TwoPi / MatrixPointCount * i + MathHelper.PiOver4;
                        float matrixRadius = 320f;
                        matrixPoints[i] = player.Center + angle.ToRotationVector2() * matrixRadius;
                    }
                }

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
            else if (modeTimer < deployTime + chargeTime) {
                int phaseTimer = modeTimer - deployTime;
                float progress = phaseTimer / (float)chargeTime;

                Context.SetChargeState(7, 0.4f + progress * 0.6f);

                //所有矩阵点蓄力特效
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < MatrixPointCount; i++) {
                        Vector2 pointPos = matrixPoints[i];
                        Vector2 toCenter = (player.Center - pointPos).SafeNormalize(Vector2.Zero);

                        if (phaseTimer % 2 == 0) {
                            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                            float dist = 40f - progress * 25f;
                            Vector2 dustPos = pointPos + angle.ToRotationVector2() * dist;
                            Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.Vortex, 0, 0, 100, default, 1.3f);
                            dust.noGravity = true;
                            dust.velocity = (pointPos - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                        }

                        if (phaseTimer % 4 == 0 && progress > 0.3f) {
                            float lineDist = 30f + progress * 150f;
                            Vector2 linePos = pointPos + toCenter * lineDist;
                            Dust dust = Dust.NewDustDirect(linePos, 1, 1, DustID.PurpleTorch, 0, 0, 100, default, 1.1f);
                            dust.noGravity = true;
                            dust.velocity = toCenter * 3f;
                        }
                    }

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
            else if (modeTimer == deployTime + chargeTime) {
                Context.ResetChargeState();
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item33 with { Pitch = 0f, Volume = 1.4f }, npc.Center);
                }

                if (!VaultUtils.isClient) {
                    for (int i = 0; i < MatrixPointCount; i++) {
                        Vector2 pointPos = matrixPoints[i];
                        Vector2 toCenter = (player.Center - pointPos).SafeNormalize(Vector2.Zero);

                        //发射多发激光(首发强化)
                        for (int j = 0; j < 2; j++) {
                            float speedMult = 1f - j * 0.3f;
                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                pointPos,
                                toCenter * (LaserSpeed * speedMult),
                                ModContent.ProjectileType<RetinazerLaser>(),
                                j == 0 ? 32 : 24,
                                0f,
                                Main.myPlayer,
                                0f,
                                j == 0 ? 1f : 0f
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
