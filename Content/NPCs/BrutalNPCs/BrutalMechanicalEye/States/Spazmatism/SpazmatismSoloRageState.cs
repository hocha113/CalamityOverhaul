using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>
    /// 魔焰眼独眼狂暴状态
    /// 当激光眼死亡后进入，攻击更加疯狂和不可预测
    /// 包含多种攻击模式的快速切换
    /// </summary>
    internal class SpazmatismSoloRageState : TwinsStateBase
    {
        public override string StateName => "SpazmatismSoloRage";

        /// <summary>
        /// 当前攻击模式
        /// </summary>
        private enum RageAttackMode
        {
            /// <summary>
            /// 疯狂冲刺，高速多次冲刺
            /// </summary>
            FrenziedDash,
            /// <summary>
            /// 火焰漩涡，围绕玩家旋转喷火
            /// </summary>
            FlameVortex,
            /// <summary>
            /// 爆发射击，快速发射大量火球
            /// </summary>
            BurstFire,
            /// <summary>
            /// 追踪冲刺,持续追踪玩家的冲刺
            /// </summary>
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
        private float DashSpeed => Context.IsMachineRebellion ? 42f : (Context.IsDeathMode ? 38f : 35f);
        private int MaxDashCount => Context.IsMachineRebellion ? 8 : (Context.IsDeathMode ? 7 : 6);
        private int DashPrepareTime => Context.IsMachineRebellion ? 15 : (Context.IsDeathMode ? 18 : 22);
        private int DashDuration => 25;
        private float VortexSpeed => Context.IsMachineRebellion ? 0.12f : (Context.IsDeathMode ? 0.1f : 0.08f);
        private int BurstFireRate => Context.IsMachineRebellion ? 4 : (Context.IsDeathMode ? 5 : 6);
        private int BurstCount => Context.IsMachineRebellion ? 20 : (Context.IsDeathMode ? 16 : 12);

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

            //持续产生狂暴粒子效果
            if (!VaultUtils.isServer && Timer % 3 == 0) {
                Dust dust = Dust.NewDustDirect(
                    npc.Center + Main.rand.NextVector2Circular(30, 30),
                    1, 1, DustID.SolarFlare, 0, 0, 100, default, 1.5f);
                dust.noGravity = true;
                dust.velocity = Main.rand.NextVector2Circular(3, 3);
            }

            //独眼狂暴模式不会自动切换出去，除非死亡
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

            //随机选择下一个模式，但避免连续相同
            RageAttackMode previousMode = currentMode;
            do {
                currentMode = (RageAttackMode)Main.rand.Next(4);
            } while (currentMode == previousMode && Main.rand.NextFloat() < 0.7f);
        }

        /// <summary>
        /// 疯狂冲刺模式 - 高速多次冲刺
        /// </summary>
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

                //即将冲刺时锁定方向
                if (phaseInCycle == prepareTime - 1) {
                    dashDirection = GetDirectionToTarget(Context);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f }, npc.Center);
                    Context.ResetChargeState();
                }
            }
            //冲刺阶段
            else {
                npc.velocity = dashDirection * DashSpeed;
                FaceVelocity(npc);

                //冲刺轨迹粒子
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 3; i++) {
                        Dust dust = Dust.NewDustDirect(
                            npc.Center - npc.velocity.SafeNormalize(Vector2.Zero) * 20f + Main.rand.NextVector2Circular(15, 15),
                            1, 1, DustID.SolarFlare, -npc.velocity.X * 0.2f, -npc.velocity.Y * 0.2f, 100, default, 1.6f);
                        dust.noGravity = true;
                    }
                }

                //冲刺结束
                if (phaseInCycle == cycleTime - 1) {
                    npc.velocity *= 0.3f;
                    attackCount++;

                    if (attackCount >= MaxDashCount) {
                        SwitchToNextMode();
                    }
                }
            }
        }

        /// <summary>
        /// 火焰漩涡模式 - 围绕玩家旋转喷火
        /// </summary>
        private void ExecuteFlameVortex(NPC npc, Player player) {
            int vortexDuration = Context.IsMachineRebellion ? 180 : (Context.IsDeathMode ? 150 : 120);

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
            //int fireRate = Context.IsMachineRebellion ? 4 : (Context.IsDeathMode ? 5 : 6);
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
            if (modeTimer % 10 == 0 && !VaultUtils.isClient) {
                Vector2 fireDir = (player.Center - npc.Center).SafeNormalize(Vector2.Zero);
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    fireDir * 10f,
                    ModContent.ProjectileType<Fireball>(),
                    32,
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

        /// <summary>
        /// 爆发射击模式 - 快速发射大量火球
        /// </summary>
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
                        //添加散射
                        float scatter = (Main.rand.NextFloat() - 0.5f) * 0.3f;
                        Vector2 shootDir = toPlayer.RotatedBy(scatter);

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            shootDir * 12f,
                            ModContent.ProjectileType<Fireball>(),
                            30,
                            0f,
                            Main.myPlayer
                        );
                    }

                    SoundEngine.PlaySound(SoundID.Item34 with { Pitch = 0.1f + attackCount * 0.02f, Volume = 0.7f }, npc.Center);
                    attackCount++;

                    //发射粒子
                    if (!VaultUtils.isServer) {
                        Vector2 toPlayer = GetDirectionToTarget(Context);
                        for (int i = 0; i < 2; i++) {
                            Dust dust = Dust.NewDustDirect(npc.Center + toPlayer * 30f, 1, 1, DustID.SolarFlare,
                                toPlayer.X * 5, toPlayer.Y * 5, 0, default, 1.3f);
                            dust.noGravity = true;
                        }
                    }
                }

                if (attackCount >= BurstCount) {
                    SwitchToNextMode();
                }
            }
        }

        /// <summary>
        /// 追踪冲刺模式 - 持续追踪玩家的冲刺
        /// </summary>
        private void ExecuteHomingDash(NPC npc, Player player) {
            int homingDuration = Context.IsMachineRebellion ? 150 : (Context.IsDeathMode ? 120 : 100);

            if (!hasPlayedModeSound) {
                hasPlayedModeSound = true;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f }, npc.Center);
            }

            //持续追踪玩家，但速度很快
            Vector2 toPlayer = GetDirectionToTarget(Context);
            float chaseSpeed = Context.IsMachineRebellion ? 8f : (Context.IsDeathMode ? 4f : 2f);
            float turnSpeed = Context.IsMachineRebellion ? 0.25f : (Context.IsDeathMode ? 0.2f : 0.15f);

            npc.velocity = Vector2.Lerp(npc.velocity, toPlayer * chaseSpeed, turnSpeed);
            FaceVelocity(npc);

            //持续喷火
            int fireRate = Context.IsMachineRebellion ? 5 : (Context.IsDeathMode ? 6 : 8);
            if (modeTimer > 30 && modeTimer % fireRate == 0 && !VaultUtils.isClient) {
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    npc.velocity.SafeNormalize(Vector2.Zero) * 12f,
                    ProjectileID.EyeFire,
                    32,
                    0f,
                    Main.myPlayer
                );
            }

            //追踪轨迹粒子
            if (!VaultUtils.isServer && modeTimer % 2 == 0) {
                for (int i = 0; i < 2; i++) {
                    Dust dust = Dust.NewDustDirect(
                        npc.Center - npc.velocity.SafeNormalize(Vector2.Zero) * 25f + Main.rand.NextVector2Circular(10, 10),
                        1, 1, DustID.SolarFlare, -npc.velocity.X * 0.1f, -npc.velocity.Y * 0.1f, 100, default, 1.5f);
                    dust.noGravity = true;
                }
            }

            //间歇性发射追踪火球
            if (modeTimer % 25 == 0 && !VaultUtils.isClient) {
                for (int i = 0; i < 3; i++) {
                    float angle = MathHelper.TwoPi / 3f * i + modeTimer * 0.1f;
                    Vector2 vel = angle.ToRotationVector2() * 6f;
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        vel,
                        ModContent.ProjectileType<Fireball>(),
                        28,
                        0f,
                        Main.myPlayer
                    );
                }
            }

            if (modeTimer >= homingDuration) {
                npc.velocity *= 0.5f;
                SwitchToNextMode();
            }
        }

        private TwinsStateContext context => Context;
    }
}
