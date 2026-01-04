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
    /// 激光眼二阶段聚焦光束状态
    /// 锁定玩家位置后发射持续的聚焦激光
    /// </summary>
    internal class RetinazerFocusedBeamState : TwinsStateBase
    {
        public override string StateName => "RetinazerFocusedBeam";

        /// <summary>
        /// 锁定阶段
        /// </summary>
        private const int LockPhase = 40;

        /// <summary>
        /// 蓄力阶段
        /// </summary>
        private const int ChargePhase = 50;

        /// <summary>
        /// 发射阶段
        /// </summary>
        private const int FirePhase = 60;

        /// <summary>
        /// 恢复阶段
        /// </summary>
        private const int RecoveryPhase = 30;

        /// <summary>
        /// 总时长
        /// </summary>
        private const int TotalDuration = LockPhase + ChargePhase + FirePhase + RecoveryPhase;

        private TwinsStateContext Context;
        private Vector2 lockedDirection;
        private Vector2 lockedTargetPos;
        private bool hasPlayedChargeSound;
        private bool hasPlayedFireSound;

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            hasPlayedChargeSound = false;
            hasPlayedFireSound = false;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //阶段1: 锁定目标
            if (Timer <= LockPhase) {
                ExecuteLockPhase(npc, player);
            }
            //阶段2: 蓄力
            else if (Timer <= LockPhase + ChargePhase) {
                ExecuteChargePhase(npc, player);
            }
            //阶段3: 发射
            else if (Timer <= LockPhase + ChargePhase + FirePhase) {
                ExecuteFirePhase(npc, player);
            }
            //阶段4: 恢复
            else {
                ExecuteRecoveryPhase(npc, player);
            }

            //状态结束
            if (Timer >= TotalDuration) {
                return new RetinazerVerticalBarrageState();
            }

            return null;
        }

        /// <summary>
        /// 锁定阶段
        /// </summary>
        private void ExecuteLockPhase(NPC npc, Player player) {
            float progress = Timer / (float)LockPhase;

            //快速移动到玩家侧面
            Vector2 targetPos = player.Center + new Vector2(npc.Center.X < player.Center.X ? -350 : 350, -100);
            MoveTo(npc, targetPos, 14f, 0.15f);

            //持续追踪玩家
            FaceTarget(npc, player.Center);

            //记录锁定方向
            lockedDirection = GetDirectionToTarget(Context);
            lockedTargetPos = player.Center;

            //预警特效
            context.SetChargeState(6, progress * 0.3f);

            //锁定指示粒子
            if (!VaultUtils.isServer && Timer % 4 == 0) {
                Vector2 dustPos = npc.Center + lockedDirection * 40f;
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.Vortex, 0, 0, 100, default, 1.2f);
                dust.noGravity = true;
                dust.velocity = lockedDirection * 3f;
            }
        }

        /// <summary>
        /// 蓄力阶段
        /// </summary>
        private void ExecuteChargePhase(NPC npc, Player player) {
            int phaseTimer = Timer - LockPhase;
            float progress = phaseTimer / (float)ChargePhase;

            //锁定位置不再移动
            npc.velocity *= 0.9f;

            //保持锁定方向
            npc.rotation = lockedDirection.ToRotation() - MathHelper.PiOver2;

            //设置蓄力状态
            context.SetChargeState(6, 0.3f + progress * 0.7f);

            //蓄力音效
            if (!hasPlayedChargeSound) {
                hasPlayedChargeSound = true;
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.2f }, npc.Center);
            }

            //能量聚集粒子
            if (!VaultUtils.isServer) {
                //从四周聚集的粒子
                if (phaseTimer % 2 == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 80f - progress * 50f;
                    Vector2 dustPos = npc.Center + angle.ToRotationVector2() * dist;
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.Vortex, 0, 0, 100, default, 1.5f + progress);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * (5f + progress * 4f);
                }

                //发射方向的能量线
                if (phaseTimer % 3 == 0 && progress > 0.3f) {
                    float lineDist = 50f + progress * 100f;
                    Vector2 dustPos = npc.Center + lockedDirection * lineDist;
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.PurpleTorch, 0, 0, 100, default, 1.8f);
                    dust.noGravity = true;
                    dust.velocity = lockedDirection * 2f;
                }

                //蓄力完成时的闪光
                if (phaseTimer == ChargePhase - 5) {
                    for (int i = 0; i < 15; i++) {
                        float angle = MathHelper.TwoPi / 15f * i;
                        Vector2 vel = angle.ToRotationVector2() * 6f;
                        Dust dust = Dust.NewDustDirect(npc.Center, 1, 1, DustID.Vortex, vel.X, vel.Y, 0, default, 2f);
                        dust.noGravity = true;
                    }
                }
            }
        }

        /// <summary>
        /// 发射阶段
        /// </summary>
        private void ExecuteFirePhase(NPC npc, Player player) {
            int phaseTimer = Timer - LockPhase - ChargePhase;
            float progress = phaseTimer / (float)FirePhase;

            //停止蓄力特效
            context.ResetChargeState();

            //保持锁定方向
            npc.rotation = lockedDirection.ToRotation() - MathHelper.PiOver2;

            //后坐力效果
            if (phaseTimer < 10) {
                npc.velocity = -lockedDirection * (10f - phaseTimer);
            }
            else {
                npc.velocity *= 0.95f;
            }

            //发射音效
            if (!hasPlayedFireSound) {
                hasPlayedFireSound = true;
                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = -0.2f, Volume = 1.2f }, npc.Center);
            }

            //持续发射激光
            int fireInterval = Context.IsMachineRebellion ? 4 : 6;
            if (phaseTimer % fireInterval == 0 && !VaultUtils.isClient) {
                //添加轻微的散射
                float scatter = (Main.rand.NextFloat() - 0.5f) * 0.1f;
                Vector2 shootDir = lockedDirection.RotatedBy(scatter);

                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    shootDir * 14f,
                    ModContent.ProjectileType<DeadLaser>(),
                    40,
                    0f,
                    Main.myPlayer
                );

                //发射时的粒子
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 dustVel = shootDir.RotatedBy((Main.rand.NextFloat() - 0.5f) * 0.3f) * 8f;
                        Dust dust = Dust.NewDustDirect(npc.Center + shootDir * 30f, 1, 1, DustID.Vortex, dustVel.X, dustVel.Y, 0, default, 1.5f);
                        dust.noGravity = true;
                    }
                }
            }

            //持续的发射轨迹粒子
            if (!VaultUtils.isServer && phaseTimer % 2 == 0) {
                Vector2 dustPos = npc.Center + lockedDirection * (40f + phaseTimer * 3f);
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.PurpleTorch, lockedDirection.X * 5, lockedDirection.Y * 5, 100, default, 1.2f);
                dust.noGravity = true;
            }
        }

        /// <summary>
        /// 恢复阶段
        /// </summary>
        private void ExecuteRecoveryPhase(NPC npc, Player player) {
            //逐渐恢复面向玩家
            FaceTarget(npc, player.Center);
            npc.velocity *= 0.95f;

            //残余粒子
            if (!VaultUtils.isServer && Timer % 5 == 0) {
                Dust dust = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(20, 20), 1, 1, DustID.Vortex, 0, -1, 100, default, 0.8f);
                dust.noGravity = true;
            }
        }

        private TwinsStateContext context => Context;
    }
}
