using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer
{
    /// <summary>一阶段激光扫射，上方悬停+扇形扫射</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.RetinazerLaserSweep, typeof(TwinsStateContext))]
    internal class RetinazerLaserSweepState : TwinsStateBase
    {
        public override string StateName => "RetinazerLaserSweep";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.RetinazerLaserSweep;

        /// <summary>进入位置阶段</summary>
        private int PositioningPhase => Context.IsDeathMode ? 25 : 30;

        private int ChargePhase => Context.IsDeathMode ? 50 : 60;

        /// <summary>扫射阶段</summary>
        private int SweepPhase => Context.IsDeathMode ? 65 : 70;

        private int RecoveryPhase => Context.IsDeathMode ? 20 : 25;

        private int TotalDuration => PositioningPhase + ChargePhase + SweepPhase + RecoveryPhase;

        private float MoveSpeed => Context.IsDeathMode ? 12f : 10f;
        private int FireInterval => Context.IsDeathMode ? 6 : 7;
        private float LaserSpeed => Context.IsDeathMode ? 13f : 11f;

        /// <summary>扫射站位，逼近到此距离即刹停，保证扇形有展开空间</summary>
        private float SweepStandoff => Context.IsDeathMode ? 380f : 440f;

        /// <summary>蓄力进度过此值即锁死瞄准，不再跟踪玩家(公平阀)</summary>
        private const float AimLockProgress = 0.75f;

        private TwinsStateContext Context;
        private Vector2 sweepStartDir;
        private bool hasFiredWarningShot;
        private bool aimLocked;
        private int comboStep;

        public RetinazerLaserSweepState() : this(0) {
        }

        public RetinazerLaserSweepState(int currentComboStep) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            hasFiredWarningShot = false;
            aimLocked = false;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            if (Timer <= PositioningPhase) {
                ExecutePositioningPhase(npc, player);
            }
            else if (Timer <= PositioningPhase + ChargePhase) {
                ExecuteChargePhase(npc, player);
            }
            else if (Timer <= PositioningPhase + ChargePhase + SweepPhase) {
                ExecuteSweepPhase(npc, player);
            }
            else {
                ExecuteRecoveryPhase(npc, player);
            }

            //状态结束，回到悬停射击继续套路循环
            if (Timer >= TotalDuration) {
                return new RetinazerHoverShootState(comboStep);
            }

            return null;
        }

        /// <summary>进入位置阶段，先升到站位之外，蓄力期的压近才读得出来</summary>
        private void ExecutePositioningPhase(NPC npc, Player player) {
            Vector2 targetPos = player.Center + new Vector2(0, -560);
            MoveTo(npc, targetPos, MoveSpeed * 0.8f, 0.12f);
            FaceTarget(npc, player.Center);

            //轻微的预警特效
            float progress = Timer / (float)PositioningPhase;
            context.SetChargeState(4, progress * 0.2f);

            //产生少量预警粒子
            if (!VaultUtils.isServer && Timer % 6 == 0) {
                Dust dust = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(20, 20), 1, 1, DustID.PurpleTorch, 0, 0, 100, default, 1f);
                dust.noGravity = true;
                dust.velocity = Vector2.Zero;
            }
        }

        private void ExecuteChargePhase(NPC npc, Player player) {
            int phaseTimer = Timer - PositioningPhase;
            float progress = phaseTimer / (float)ChargePhase;

            //压近到射击站位就刹住，末段静止即开火前那一拍；玩家贴上来则被推开
            Vector2 toPlayer = player.Center - npc.Center;
            Vector2 approachDir = toPlayer.SafeNormalize(Vector2.UnitY);
            float approach = MathHelper.Clamp((toPlayer.Length() - SweepStandoff) / 150f, -0.5f, 1f);
            npc.velocity = approachDir * MoveSpeed * approach;

            //末 1/4 锁死扇形轴线，给玩家离开中心的窗口
            if (progress <= AimLockProgress) {
                sweepStartDir = approachDir;
                FaceTarget(npc, player.Center);
            }
            else {
                if (!aimLocked) {
                    aimLocked = true;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.55f, Volume = 0.8f }, npc.Center);
                    }
                }
                npc.rotation = sweepStartDir.ToRotation() - MathHelper.PiOver2;
            }

            context.SetChargeState(4, 0.2f + progress * 0.8f);

            //蓄力粒子效果逐渐增强
            if (!VaultUtils.isServer) {
                //聚集粒子
                if (phaseTimer % 3 == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 100f - progress * 60f;
                    Vector2 dustPos = npc.Center + angle.ToRotationVector2() * dist;
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.Vortex, 0, 0, 100, default, 1.6f + progress);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * (4f + progress * 3f);
                }

                //扫射范围预警线
                if (phaseTimer > ChargePhase / 2 && phaseTimer % 4 == 0) {
                    float spreadAngle = MathHelper.PiOver4;
                    for (int side = -1; side <= 1; side += 2) {
                        Vector2 lineDir = sweepStartDir.RotatedBy(spreadAngle * side);
                        float lineDist = 50f + (progress - 0.5f) * 2f * 300f;
                        Vector2 dustPos = npc.Center + lineDir * lineDist;
                        Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.PurpleTorch, 0, 0, 150, default, 1.5f);
                        dust.noGravity = true;
                        dust.velocity = lineDir * 2f;
                    }
                }
            }

            if (phaseTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.3f, Volume = 0.7f }, npc.Center);
            }

            //蓄力完成前的预警射击
            if (phaseTimer == ChargePhase - 10 && !hasFiredWarningShot) {
                hasFiredWarningShot = true;
                SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.5f, Volume = 0.5f }, npc.Center);

                //发射预警激光(不造成伤害的视觉效果)
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 20; i++) {
                        float t = i / 19f;
                        float angle = MathHelper.Lerp(-MathHelper.PiOver4, MathHelper.PiOver4, t);
                        Vector2 dir = sweepStartDir.RotatedBy(angle);
                        Vector2 dustPos = npc.Center + dir * 60f;
                        Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.PurpleTorch, dir.X * 8, dir.Y * 8, 0, default, 1.8f);
                        dust.noGravity = true;
                        dust.fadeIn = 1.2f;
                    }
                }
            }
        }

        /// <summary>扫射阶段</summary>
        private void ExecuteSweepPhase(NPC npc, Player player) {
            int phaseTimer = Timer - PositioningPhase - ChargePhase;
            float progress = phaseTimer / (float)SweepPhase;

            context.ResetChargeState();

            //使用缓动函数使扫射更流畅
            float easedProgress = EaseInOutSine(progress);
            float sweepAngle = MathHelper.Lerp(-MathHelper.PiOver4, MathHelper.PiOver4, easedProgress);

            //更新朝向
            Vector2 currentDir = sweepStartDir.RotatedBy(sweepAngle);
            npc.rotation = currentDir.ToRotation() - MathHelper.PiOver2;

            //锁位稳住，玩家逼近则被推开，扇形始终有展开距离
            npc.velocity *= 0.95f;
            Vector2 fromPlayer = npc.Center - player.Center;
            float distToPlayer = fromPlayer.Length();
            if (distToPlayer < SweepStandoff) {
                npc.velocity += fromPlayer.SafeNormalize(Vector2.UnitY) * ((SweepStandoff - distToPlayer) * 0.02f);
            }

            if (phaseTimer % FireInterval == 0 && !VaultUtils.isClient) {
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center + currentDir * 40f,
                    currentDir * LaserSpeed,
                    ModContent.ProjectileType<RetinazerLaser>(),
                    20,
                    0f,
                    Main.myPlayer
                );
                SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.2f + progress * 0.3f, Volume = 0.8f }, npc.Center);
            }

            //扫射轨迹粒子
            if (!VaultUtils.isServer && phaseTimer % 2 == 0) {
                Vector2 dustPos = npc.Center + currentDir * 50f;
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.Vortex, currentDir.X * 3, currentDir.Y * 3, 100, default, 1.2f);
                dust.noGravity = true;
            }
        }

        private void ExecuteRecoveryPhase(NPC npc, Player player) {
            //逐渐恢复面向玩家
            FaceTarget(npc, player.Center);

            //轻微后退
            Vector2 backDir = (npc.Center - player.Center).SafeNormalize(Vector2.Zero);
            npc.velocity = Vector2.Lerp(npc.velocity, backDir * 3f, 0.1f);

            if (!VaultUtils.isServer && Timer % 5 == 0) {
                Dust dust = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(20, 20), 1, 1, DustID.PurpleTorch, 0, -1, 100, default, 0.8f);
                dust.noGravity = true;
            }
        }

        /// <summary>正弦缓入缓出函数</summary>
        private static float EaseInOutSine(float t) {
            return -(float)Math.Cos(Math.PI * t) / 2f + 0.5f;
        }

        private TwinsStateContext context => Context;
    }
}
