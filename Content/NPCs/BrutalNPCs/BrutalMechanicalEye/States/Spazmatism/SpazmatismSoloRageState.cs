using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>魔焰眼独眼狂暴：激光眼死后切入，四模式快切循环</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.SpazmatismSoloRage, typeof(TwinsStateContext))]
    internal class SpazmatismSoloRageState : TwinsStateBase
    {
        public override string StateName => "SpazmatismSoloRage";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.SpazmatismSoloRage;

        /// <summary>狂暴攻击模式</summary>
        private enum RageAttackMode
        {
            /// <summary>疯狂冲刺</summary>
            FrenziedDash,
            /// <summary>火焰漩涡</summary>
            FlameVortex,
            /// <summary>爆发射击</summary>
            BurstFire,
            /// <summary>追踪冲刺</summary>
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

        //难度调整参数
        private float DashSpeed => Context.IsDeathMode ? 36f : 33f;
        private int MaxDashCount => Context.IsDeathMode ? 5 : 4;
        private int DashPrepareTime => Context.IsDeathMode ? 22 : 26;
        private int DashDuration => 25;
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

            //清除狂暴触发标记
            context.SoloRageJustTriggered = false;

            //狂暴觉醒演出:火焰能量自四周向眼体倒灌
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
            modeTimer++;

            //根据当前模式执行不同的攻击
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

            //持续产生狂暴粒子效果:暴怒余温火星
            if (!VaultUtils.isServer && Timer % 3 == 0) {
                PRTLoader.NewParticle<PRT_TwinsSpark>(
                    npc.Center + Main.rand.NextVector2Circular(30, 30),
                    Main.rand.NextVector2Circular(2.5f, 2.5f) - new Vector2(0, 1.5f),
                    Color.White, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(16, 1);
            }

            //独眼狂暴模式不会自动切换出去，除非死亡
            return null;
        }

        /// <summary>狂暴循环：连冲→爆发→追踪→漩涡</summary>
        private static readonly RageAttackMode[] RageComboSequence =
        [
            RageAttackMode.FrenziedDash,
            RageAttackMode.BurstFire,
            RageAttackMode.HomingDash,
            RageAttackMode.FlameVortex
        ];

        /// <summary>切下一攻击模式</summary>
        private void SwitchToNextMode() {
            totalAttacks++;
            modeTimer = 0;
            attackCount = 0;
            hasPlayedModeSound = false;
            //切换模式时默认禁用碰撞伤害
            DisableContactDamage(Context.Npc);

            //按固定套路循环切换模式
            currentMode = RageComboSequence[totalAttacks % RageComboSequence.Length];
        }

        /// <summary>疯狂冲刺：高速多段 dash</summary>
        private void ExecuteFrenziedDash(NPC npc, Player player) {
            int prepareTime = DashPrepareTime;
            int dashTime = DashDuration;
            int cycleTime = prepareTime + dashTime;
            int phaseInCycle = modeTimer % cycleTime;

            //准备阶段
            if (phaseInCycle < prepareTime) {
                //减速并面向玩家
                npc.velocity *= 0.9f;
                FaceTarget(npc, player.Center);

                //蓄力特效
                float progress = phaseInCycle / (float)prepareTime;
                Context.SetChargeState(1, progress);

                //蓄力粒子
                if (!VaultUtils.isServer && phaseInCycle % 2 == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 60f - progress * 40f;
                    Vector2 dustPos = npc.Center + angle.ToRotationVector2() * dist;
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.SolarFlare, 0, 0, 100, default, 1.8f);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                }

                //即将冲刺时锁定方向(带预判)
                if (phaseInCycle == prepareTime - 1) {
                    Vector2 predicted = TwinsMotion.PredictTarget(player, npc.Center, DashSpeed, 0.55f);
                    dashDirection = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f }, npc.Center);
                    Context.ResetChargeState();
                }
            }
            //冲刺阶段
            else {
                //冲刺时启用碰撞伤害
                EnableContactDamage(npc);

                //起步瞬间爆发+音爆
                if (phaseInCycle == prepareTime) {
                    TwinsMotion.DashLaunch(npc, dashDirection, DashSpeed, spazTheme: true, boomStrength: 1.2f);
                }
                else {
                    //全速段微弧追踪
                    TwinsMotion.CurveChase(npc, player.Center, DashSpeed, 0.014f);
                }
                FaceVelocity(npc);
                Context.PushDashVisuals(1f, 1f);

                //冲刺轨迹粒子
                if (!VaultUtils.isServer && phaseInCycle % 2 == 0) {
                    PRTLoader.NewParticle<PRT_TwinsSpark>(
                        npc.Center - npc.velocity.SafeNormalize(Vector2.Zero) * 30f + Main.rand.NextVector2Circular(14, 14),
                        -npc.velocity * 0.15f, Color.White, Main.rand.NextFloat(1.1f, 1.7f))?.Configure(15, 1);
                }

                //冲刺结束:急停甩头
                if (phaseInCycle == cycleTime - 1) {
                    TwinsMotion.BrakeAndWhip(npc, player.Center, 0.4f, 0.5f);
                    attackCount++;
                    //冲刺结束禁用碰撞伤害
                    DisableContactDamage(npc);

                    if (attackCount >= MaxDashCount) {
                        SwitchToNextMode();
                    }
                }
            }
        }

        /// <summary>火焰漩涡：绕玩家旋转喷火</summary>
        private void ExecuteFlameVortex(NPC npc, Player player) {
            //缩短漩涡持续时间，避免动作过于凝滞
            int vortexDuration = Context.IsDeathMode ? 110 : 95;

            if (!hasPlayedModeSound) {
                hasPlayedModeSound = true;
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f, Volume = 1.2f }, npc.Center);
            }

            //围绕玩家旋转
            vortexAngle += VortexSpeed;
            float radius = 680f + (float)Math.Sin(modeTimer * 0.05f) * 120f; //半径会波动
            Vector2 targetPos = player.Center + vortexAngle.ToRotationVector2() * radius;

            //快速移动到目标位置
            npc.Center = Vector2.Lerp(npc.Center, targetPos, 0.15f);

            //面向玩家
            FaceTarget(npc, player.Center);

            //持续喷火
            //操你妈躲都躲不开，注释了
            //int fireRate = Context.IsDeathMode ? 5 : 6;
            //if (modeTimer % fireRate == 0 && !VaultUtils.isClient) {
            //    Vector2 fireDir = (player.Center - npc.Center).SafeNormalize(Vector2.Zero);
            //    Projectile.NewProjectile(
            //        npc.GetSource_FromAI(),
            //        npc.Center,
            //        fireDir * 14f,
            //        ProjectileID.EyeFire,
            //        35,
            //        0f,
            //        Main.myPlayer
            //    );
            //}

            //间歇性发射火球
            if (modeTimer % 14 == 0 && !VaultUtils.isClient) {
                Vector2 fireDir = (player.Center - npc.Center).SafeNormalize(Vector2.Zero);
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    fireDir * 10f,
                    ModContent.ProjectileType<Fireball>(),
                    26,
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

        /// <summary>爆发射击：快射大量火球</summary>
        private void ExecuteBurstFire(NPC npc, Player player) {
            if (!hasPlayedModeSound) {
                hasPlayedModeSound = true;
                SoundEngine.PlaySound(SoundID.Item45 with { Pitch = 0.3f }, npc.Center);
            }

            //悬停在玩家附近
            Vector2 hoverPos = player.Center + new Vector2(npc.Center.X < player.Center.X ? -300 : 300, -200);
            MoveTo(npc, hoverPos, 12f, 0.08f);
            FaceTarget(npc, player.Center);

            //蓄力特效
            if (modeTimer < 30) {
                Context.SetChargeState(3, modeTimer / 30f);

                //能量聚集粒子
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
                            24,
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

        /// <summary>追踪冲刺：持续追玩家 dash</summary>
        private void ExecuteHomingDash(NPC npc, Player player) {
            int homingDuration = Context.IsDeathMode ? 120 : 100;

            if (!hasPlayedModeSound) {
                hasPlayedModeSound = true;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f }, npc.Center);
                //追踪冲刺模式启用碰撞伤害
                EnableContactDamage(npc);
            }

            //弧线穷追:速度恒定+限转速，缠斗压制
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
                        22,
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
