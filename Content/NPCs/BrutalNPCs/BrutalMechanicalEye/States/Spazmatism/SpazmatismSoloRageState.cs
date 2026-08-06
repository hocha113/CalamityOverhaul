using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>魔焰眼独眼狂暴，激光眼死后切入，四模式快切循环</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.SpazmatismSoloRage, typeof(TwinsStateContext))]
    internal class SpazmatismSoloRageState : TwinsStateBase
    {
        public override string StateName => "SpazmatismSoloRage";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.SpazmatismSoloRage;

        private enum RageAttackMode
        {
            FrenziedDash,
            FlameVortex,
            BurstFire,
            HomingDash
        }

        private TwinsStateContext Context;
        private RageAttackMode currentMode;
        private int modeTimer;
        private int attackCount;
        private int totalAttacks;
        private Vector2 dashDirection;
        private float vortexAngle;
        private bool hasPlayedModeSound;

        /// <summary>换招连接节拍剩余帧，让段落间隔被看见</summary>
        private int modeTransitionTimer;

        private int ModeTransitionTime => Context.IsDeathMode ? 14 : 18;

        private float DashSpeed => Context.IsDeathMode ? 42f : 38f;
        private int MaxDashCount => Context.IsDeathMode ? 5 : 4;
        private int DashPrepareTime => Context.IsDeathMode ? 26 : 30;
        private int DashDuration => 16;

        /// <summary>每次冲刺后的复位喘息，无伤</summary>
        private int DashRecoverTime => Context.IsDeathMode ? 10 : 12;
        private float VortexSpeed => Context.IsDeathMode ? 0.1f : 0.08f;
        private int BurstFireRate => Context.IsDeathMode ? 7 : 8;
        private int BurstCount => Context.IsDeathMode ? 12 : 10;

        public SpazmatismSoloRageState() {
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            currentMode = RageAttackMode.FrenziedDash;
            modeTimer = 0;
            attackCount = 0;
            totalAttacks = 0;
            vortexAngle = 0f;
            hasPlayedModeSound = false;
            modeTransitionTimer = 0;

            context.SoloRageJustTriggered = false;

            //狂暴觉醒倒灌
            if (!VaultUtils.isServer) {
                NPC npc = context.Npc;
                for (int i = 0; i < 26; i++) {
                    Vector2 spawnPos = npc.Center + Main.rand.NextVector2CircularEdge(220f, 220f);
                    PRTLoader.NewParticle<PRT_TwinsSpark>(spawnPos, (npc.Center - spawnPos) * 0.07f,
                        Color.White, Main.rand.NextFloat(1.2f, 2f))?.Configure(28, 1);
                }
                PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero, TwinsMotion.SpazColor, 1.1f)?
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
                DisableContactDamage(npc);
                npc.velocity *= 0.88f;
                FaceTarget(npc, player.Center);
                Context.ResetChargeState();

                if (!VaultUtils.isServer && modeTransitionTimer % 4 == 0) {
                    PRTLoader.NewParticle<PRT_Smoke>(npc.Center + Main.rand.NextVector2Circular(22, 22),
                        new Vector2(0, -1.6f) + Main.rand.NextVector2Circular(0.7f, 0.7f),
                        TwinsMotion.SpazColor * 0.5f, Main.rand.NextFloat(0.6f, 1f))?
                        .Configure(30, 0.5f, 0.02f, false, 0f);
                }
                return null;
            }

            modeTimer++;

            switch (currentMode) {
                case RageAttackMode.FrenziedDash:
                    ExecuteFrenziedDash(npc, player);
                    break;
                case RageAttackMode.FlameVortex:
                    ExecuteFlameVortex(npc, player);
                    break;
                case RageAttackMode.BurstFire:
                    ExecuteBurstFire(npc, player);
                    break;
                case RageAttackMode.HomingDash:
                    ExecuteHomingDash(npc, player);
                    break;
            }

            //狂暴余温火星
            if (!VaultUtils.isServer && Timer % 3 == 0) {
                PRTLoader.NewParticle<PRT_TwinsSpark>(
                    npc.Center + Main.rand.NextVector2Circular(30, 30),
                    Main.rand.NextVector2Circular(2.5f, 2.5f) - new Vector2(0, 1.5f),
                    Color.White, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(16, 1);
            }

            return null;
        }

        /// <summary>狂暴循环，连冲→爆发→追踪→漩涡</summary>
        private static readonly RageAttackMode[] RageComboSequence =
        [
            RageAttackMode.FrenziedDash,
            RageAttackMode.BurstFire,
            RageAttackMode.HomingDash,
            RageAttackMode.FlameVortex
        ];

        private void SwitchToNextMode() {
            totalAttacks++;
            modeTimer = 0;
            attackCount = 0;
            hasPlayedModeSound = false;
            modeTransitionTimer = ModeTransitionTime;
            DisableContactDamage(Context.Npc);

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.5f, Volume = 0.9f }, Context.Npc.Center);
            }

            currentMode = RageComboSequence[totalAttacks % RageComboSequence.Length];
        }

        /// <summary>疯狂冲刺，高速多段 dash</summary>
        private void ExecuteFrenziedDash(NPC npc, Player player) {
            int prepareTime = DashPrepareTime;
            int dashTime = DashDuration;
            int recoverTime = DashRecoverTime;
            int cycleTime = prepareTime + dashTime + recoverTime;
            int phaseInCycle = modeTimer % cycleTime;

            if (phaseInCycle < prepareTime) {
                npc.velocity *= 0.9f;
                FaceTarget(npc, player.Center);

                float progress = phaseInCycle / (float)prepareTime;
                Context.SetChargeState(1, progress);

                if (!VaultUtils.isServer && phaseInCycle % 2 == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 60f - progress * 40f;
                    Vector2 dustPos = npc.Center + angle.ToRotationVector2() * dist;
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.SolarFlare, 0, 0, 100, default, 1.8f);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                }

                //冲刺前锁向+预判
                if (phaseInCycle == prepareTime - 1) {
                    Vector2 predicted = TwinsMotion.PredictTarget(player, npc.Center, DashSpeed, 0.55f);
                    dashDirection = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f }, npc.Center);
                    Context.ResetChargeState();
                }
            }
            else if (phaseInCycle < prepareTime + dashTime) {
                EnableContactDamageIfFast(npc);

                //起步爆发+音爆
                if (phaseInCycle == prepareTime) {
                    TwinsMotion.DashLaunch(npc, dashDirection, DashSpeed, spazTheme: true, boomStrength: 1.2f);
                }
                else {
                    //全速微弧
                    TwinsMotion.CurveChase(npc, player.Center, DashSpeed, 0.014f);
                }
                FaceVelocity(npc);
                Context.PushDashVisuals(1f, 1f);

                if (!VaultUtils.isServer && phaseInCycle % 2 == 0) {
                    PRTLoader.NewParticle<PRT_TwinsSpark>(
                        npc.Center - npc.velocity.SafeNormalize(Vector2.Zero) * 30f + Main.rand.NextVector2Circular(14, 14),
                        -npc.velocity * 0.15f, Color.White, Main.rand.NextFloat(1.1f, 1.7f))?.Configure(15, 1);
                }
            }
            else {
                //急停甩头后复位喘息，本段计入次数
                DisableContactDamage(npc);

                if (phaseInCycle == prepareTime + dashTime) {
                    TwinsMotion.BrakeAndWhip(npc, player.Center, 0.4f, 0.5f);
                    attackCount++;
                }
                else {
                    Vector2 resetPos = player.Center
                        + new Vector2(npc.Center.X < player.Center.X ? -340 : 340, -200);
                    TwinsMotion.SpringHover(npc, resetPos, 0.016f, 0.1f, 22f);
                    FaceTarget(npc, player.Center);
                }
                Context.PushDashVisuals(0.35f, 0.6f);

                if (phaseInCycle == cycleTime - 1 && attackCount >= MaxDashCount) {
                    SwitchToNextMode();
                }
            }
        }

        /// <summary>火焰漩涡，绕玩家旋转喷火</summary>
        private void ExecuteFlameVortex(NPC npc, Player player) {
            //缩短漩涡时长
            int vortexDuration = Context.IsDeathMode ? 110 : 95;

            if (!hasPlayedModeSound) {
                hasPlayedModeSound = true;
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f, Volume = 1.2f }, npc.Center);
            }

            vortexAngle += VortexSpeed;
            float radius = 680f + (float)Math.Sin(modeTimer * 0.05f) * 120f;  //半径会波动
            Vector2 targetPos = player.Center + vortexAngle.ToRotationVector2() * radius;

            npc.Center = Vector2.Lerp(npc.Center, targetPos, 0.15f);

            FaceTarget(npc, player.Center);

            //操你妈躲都躲不开，注释了
            //int fireRate = Context.IsDeathMode ? 5 : 6;
            //if (modeTimer % fireRate == 0 && !VaultUtils.isClient) {
            //    Vector2 fireDir = (player.Center - npc.Center).SafeNormalize(Vector2.Zero);
            //    Projectile.NewProjectile(
            //        npc.GetSource_FromAI(),
            //        npc.Center,
            //fireDir * 14f,
            //        ProjectileID.EyeFire,
            //35,
            //0f,
            //        Main.myPlayer
            //);
            //}

            //间歇性发射火球
            if (modeTimer % 14 == 0 && !VaultUtils.isClient) {
                Vector2 fireDir = (player.Center - npc.Center).SafeNormalize(Vector2.Zero);
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    fireDir * 10f,
                    ModContent.ProjectileType<Fireball>(),
                    22,
                    0f,
                    Main.myPlayer
                );
            }

            //旋转粒子特效
            if (!VaultUtils.isServer && modeTimer % 2 == 0) {
                Vector2 tangent = (vortexAngle + MathHelper.PiOver2).ToRotationVector2();
                Dust dust = Dust.NewDustDirect(npc.Center, 1, 1, DustID.SolarFlare,
                    tangent.X * 3, tangent.Y * 3, 100, default, 1.4f);
                dust.noGravity = true;
            }

            if (modeTimer >= vortexDuration) {
                SwitchToNextMode();
            }
        }

        /// <summary>爆发射击，快射大量火球</summary>
        private void ExecuteBurstFire(NPC npc, Player player) {
            if (!hasPlayedModeSound) {
                hasPlayedModeSound = true;
                SoundEngine.PlaySound(SoundID.Item45 with { Pitch = 0.3f }, npc.Center);
            }

            //悬停在玩家附近
            Vector2 hoverPos = player.Center + new Vector2(npc.Center.X < player.Center.X ? -300 : 300, -200);
            MoveTo(npc, hoverPos, 12f, 0.08f);
            FaceTarget(npc, player.Center);

            if (modeTimer < 30) {
                Context.SetChargeState(3, modeTimer / 30f);

                if (!VaultUtils.isServer && modeTimer % 2 == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 80f - (modeTimer / 30f) * 50f;
                    Vector2 dustPos = npc.Center + angle.ToRotationVector2() * dist;
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.SolarFlare, 0, 0, 100, default, 1.6f);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                }
            }
            else {
                Context.ResetChargeState();

                //快速发射火球
                if (modeTimer % BurstFireRate == 0 && attackCount < BurstCount) {
                    if (!VaultUtils.isClient) {
                        Vector2 toPlayer = GetDirectionToTarget(Context);
                        //固定扇形散射，基于当前攻击计数确定角度
                        float scatterRange = 0.3f;
                        float scatter = -scatterRange / 2f + scatterRange * (attackCount / (float)BurstCount);
                        Vector2 shootDir = toPlayer.RotatedBy(scatter);

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            shootDir * 12f,
                            ModContent.ProjectileType<Fireball>(),
                            20,
                            0f,
                            Main.myPlayer
                        );
                    }

                    SoundEngine.PlaySound(SoundID.Item34 with { Pitch = 0.1f + attackCount * 0.02f, Volume = 0.7f }, npc.Center);
                    attackCount++;

                    //后坐力位移与喷口闪光
                    Vector2 recoilDir = GetDirectionToTarget(Context);
                    npc.velocity -= recoilDir * 3.5f;
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 3; i++) {
                            PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center + recoilDir * 36f,
                                recoilDir.RotatedBy(Main.rand.NextFloat(-0.35f, 0.35f)) * Main.rand.NextFloat(3f, 6f),
                                Color.White, Main.rand.NextFloat(0.9f, 1.4f))?.Configure(13, 1);
                        }
                    }
                }

                if (attackCount >= BurstCount) {
                    SwitchToNextMode();
                }
            }
        }

        /// <summary>追踪冲刺，持续追玩家 dash</summary>
        private void ExecuteHomingDash(NPC npc, Player player) {
            int homingDuration = Context.IsDeathMode ? 120 : 100;

            if (!hasPlayedModeSound) {
                hasPlayedModeSound = true;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f }, npc.Center);
                //追踪冲刺模式启用碰撞伤害
                EnableContactDamage(npc);
            }

            //弧线穷追
            float chaseSpeed = Context.IsDeathMode ? 9.5f : 7.5f;
            float maxTurn = Context.IsDeathMode ? 0.055f : 0.045f;
            TwinsMotion.CurveChase(npc, player.Center, chaseSpeed, maxTurn);
            FaceVelocity(npc);
            Context.PushDashVisuals(0.3f, 0.4f);

            //持续喷吐火舌
            int fireRate = Context.IsDeathMode ? 8 : 10;
            if (modeTimer > 30 && modeTimer % fireRate == 0 && !VaultUtils.isClient) {
                Vector2 fireDir = npc.velocity.SafeNormalize(Vector2.UnitY);
                for (int i = -1; i <= 1; i += 2) {
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center + fireDir * 38f,
                        fireDir.RotatedBy(i * 0.1f) * 12f,
                        ModContent.ProjectileType<CursedFlameJet>(),
                        26,
                        0f,
                        Main.myPlayer
                    );
                }
            }

            //追踪轨迹粒子
            if (!VaultUtils.isServer && modeTimer % 2 == 0) {
                PRTLoader.NewParticle<PRT_TwinsSpark>(
                    npc.Center - npc.velocity.SafeNormalize(Vector2.Zero) * 28f + Main.rand.NextVector2Circular(10, 10),
                    -npc.velocity * 0.12f, Color.White, Main.rand.NextFloat(1f, 1.5f))?.Configure(14, 1);
            }

            //间歇性发射追踪火球
            if (modeTimer % 30 == 0 && !VaultUtils.isClient) {
                for (int i = 0; i < 3; i++) {
                    float angle = MathHelper.TwoPi / 3f * i + modeTimer * 0.1f;
                    Vector2 vel = angle.ToRotationVector2() * 6f;
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        vel,
                        ModContent.ProjectileType<Fireball>(),
                        20,
                        0f,
                        Main.myPlayer
                    );
                }
            }

            if (modeTimer >= homingDuration) {
                npc.velocity *= 0.5f;
                //追踪冲刺结束禁用碰撞伤害
                DisableContactDamage(npc);
                SwitchToNextMode();
            }
        }

        private TwinsStateContext context => Context;
    }
}
