using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer
{
    /// <summary>
    /// 激光眼一阶段激光扫射状态
    /// 在玩家上方悬停并发射扫射激光
    /// </summary>
    internal class RetinazerLaserSweepState : TwinsStateBase
    {
        public override string StateName => "RetinazerLaserSweep";

        /// <summary>
        /// 进入位置阶段
        /// </summary>
        private const int PositioningPhase = 30;

        /// <summary>
        /// 蓄力阶段
        /// </summary>
        private const int ChargePhase = 60;

        /// <summary>
        /// 扫射阶段
        /// </summary>
        private const int SweepPhase = 70;

        /// <summary>
        /// 恢复阶段
        /// </summary>
        private const int RecoveryPhase = 25;

        /// <summary>
        /// 总时长
        /// </summary>
        private const int TotalDuration = PositioningPhase + ChargePhase + SweepPhase + RecoveryPhase;

        private float MoveSpeed => Context.IsMachineRebellion ? 14f : 10f;

        private TwinsStateContext Context;
        private Vector2 sweepStartDir;
        private bool hasFiredWarningShot;

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            hasFiredWarningShot = false;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //阶段1: 进入位置
            if (Timer <= PositioningPhase) {
                ExecutePositioningPhase(npc, player);
            }
            //阶段2: 蓄力
            else if (Timer <= PositioningPhase + ChargePhase) {
                ExecuteChargePhase(npc, player);
            }
            //阶段3: 扫射
            else if (Timer <= PositioningPhase + ChargePhase + SweepPhase) {
                ExecuteSweepPhase(npc, player);
            }
            //阶段4: 恢复
            else {
                ExecuteRecoveryPhase(npc, player);
            }

            //状态结束
            if (Timer >= TotalDuration) {
                return new RetinazerHoverShootState();
            }

            return null;
        }

        /// <summary>
        /// 进入位置阶段
        /// </summary>
        private void ExecutePositioningPhase(NPC npc, Player player) {
            Vector2 targetPos = player.Center + new Vector2(0, -400);
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

        /// <summary>
        /// 蓄力阶段
        /// </summary>
        private void ExecuteChargePhase(NPC npc, Player player) {
            int phaseTimer = Timer - PositioningPhase;
            float progress = phaseTimer / (float)ChargePhase;

            //减速并锁定位置
            npc.velocity *= 0.92f;

            //记录扫射起始方向
            sweepStartDir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
            FaceTarget(npc, player.Center);

            //设置蓄力状态
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

            //蓄力音效
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

        /// <summary>
        /// 扫射阶段
        /// </summary>
        private void ExecuteSweepPhase(NPC npc, Player player) {
            int phaseTimer = Timer - PositioningPhase - ChargePhase;
            float progress = phaseTimer / (float)SweepPhase;

            //停止蓄力特效
            context.ResetChargeState();

            //使用缓动函数使扫射更流畅
            float easedProgress = EaseInOutSine(progress);
            float sweepAngle = MathHelper.Lerp(-MathHelper.PiOver4, MathHelper.PiOver4, easedProgress);

            //更新朝向
            Vector2 currentDir = sweepStartDir.RotatedBy(sweepAngle);
            npc.rotation = currentDir.ToRotation() - MathHelper.PiOver2;

            //保持位置稳定
            npc.velocity *= 0.95f;

            //发射激光
            int fireInterval = Context.IsMachineRebellion ? 5 : 7;
            if (phaseTimer % fireInterval == 0 && !VaultUtils.isClient) {
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    currentDir * 11f,
                    ProjectileID.DeathLaser,
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

        /// <summary>
        /// 恢复阶段
        /// </summary>
        private void ExecuteRecoveryPhase(NPC npc, Player player) {
            //逐渐恢复面向玩家
            FaceTarget(npc, player.Center);

            //轻微后退
            Vector2 backDir = (npc.Center - player.Center).SafeNormalize(Vector2.Zero);
            npc.velocity = Vector2.Lerp(npc.velocity, backDir * 3f, 0.1f);

            //残余粒子
            if (!VaultUtils.isServer && Timer % 5 == 0) {
                Dust dust = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(20, 20), 1, 1, DustID.PurpleTorch, 0, -1, 100, default, 0.8f);
                dust.noGravity = true;
            }
        }

        /// <summary>
        /// 正弦缓入缓出函数
        /// </summary>
        private static float EaseInOutSine(float t) {
            return -(float)Math.Cos(Math.PI * t) / 2f + 0.5f;
        }

        private TwinsStateContext context => Context;
    }
}
