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
    /// 魔焰眼二阶段影分身冲刺状态
    /// 产生多个残影同时向玩家冲刺
    /// </summary>
    internal class SpazmatismShadowDashState : TwinsStateBase
    {
        public override string StateName => "SpazmatismShadowDash";

        /// <summary>
        /// 聚集阶段
        /// </summary>
        private int GatherPhase => Context.IsDeathMode ? 30 : 40;

        /// <summary>
        /// 分身生成阶段
        /// </summary>
        private int SplitPhase => Context.IsDeathMode ? 25 : 30;

        /// <summary>
        /// 蓄力阶段
        /// </summary>
        private int ChargePhase => Context.IsDeathMode ? 28 : 35;

        /// <summary>
        /// 冲刺阶段
        /// </summary>
        private int DashPhase => Context.IsDeathMode ? 40 : 45;

        /// <summary>
        /// 恢复阶段
        /// </summary>
        private int RecoveryPhase => Context.IsDeathMode ? 25 : 30;

        /// <summary>
        /// 总时长
        /// </summary>
        private int TotalDuration => GatherPhase + SplitPhase + ChargePhase + DashPhase + RecoveryPhase;

        /// <summary>
        /// 分身数量
        /// </summary>
        private int ShadowCount => Context.IsMachineRebellion ? 5 : (Context.IsDeathMode ? 4 : 3);

        private TwinsStateContext Context;
        private Vector2[] shadowPositions;
        private Vector2[] shadowDirections;
        private Vector2 centerPoint;
        private float dashSpeed;
        private bool hasDashed;

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            shadowPositions = new Vector2[ShadowCount];
            shadowDirections = new Vector2[ShadowCount];
            dashSpeed = context.IsMachineRebellion ? 28f : (context.IsDeathMode ? 26f : 22f);
            hasDashed = false;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //检测独眼狂暴模式触发
            if (context.SoloRageJustTriggered) {
                return new SpazmatismSoloRageState();
            }

            Timer++;

            //阶段1: 聚集能量
            if (Timer <= GatherPhase) {
                ExecuteGatherPhase(npc, player);
            }
            //阶段2: 分身生成
            else if (Timer <= GatherPhase + SplitPhase) {
                ExecuteSplitPhase(npc, player);
            }
            //阶段3: 蓄力
            else if (Timer <= GatherPhase + SplitPhase + ChargePhase) {
                ExecuteChargePhase(npc, player);
            }
            //阶段4: 冲刺
            else if (Timer <= GatherPhase + SplitPhase + ChargePhase + DashPhase) {
                ExecuteDashPhase(npc, player);
            }
            //阶段5: 恢复
            else {
                ExecuteRecoveryPhase(npc, player);
            }

            //状态结束
            if (Timer >= TotalDuration) {
                //独眼模式下切换到狂暴状态
                if (context.IsSoloRageMode) {
                    return new SpazmatismSoloRageState();
                }
                return new SpazmatismFlameChaseState();
            }

            return null;
        }

        /// <summary>
        /// 聚集阶段
        /// </summary>
        private void ExecuteGatherPhase(NPC npc, Player player) {
            float progress = Timer / (float)GatherPhase;

            //移动到玩家附近
            Vector2 targetPos = player.Center + new Vector2(0, -300);
            MoveTo(npc, targetPos, 14f, 0.1f);
            FaceTarget(npc, player.Center);

            //设置蓄力状态
            context.SetChargeState(8, progress * 0.3f);

            //聚集火焰粒子
            if (!VaultUtils.isServer && Timer % 2 == 0) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = 80f - progress * 40f;
                Vector2 dustPos = npc.Center + angle.ToRotationVector2() * dist;
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.SolarFlare, 0, 0, 100, default, 1.5f + progress);
                dust.noGravity = true;
                dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
            }

            //聚集音效
            if (Timer == 1) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f, Volume = 0.8f }, npc.Center);
            }
        }

        /// <summary>
        /// 分身生成阶段
        /// </summary>
        private void ExecuteSplitPhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase;
            float progress = phaseTimer / (float)SplitPhase;

            //记录中心点
            centerPoint = player.Center;

            //减速悬停
            npc.velocity *= 0.9f;
            FaceTarget(npc, player.Center);

            //计算分身位置(围绕玩家)
            float baseRadius = 400f;
            for (int i = 0; i < ShadowCount; i++) {
                float angle = MathHelper.TwoPi / ShadowCount * i + MathHelper.PiOver2;
                shadowPositions[i] = centerPoint + angle.ToRotationVector2() * baseRadius;
                shadowDirections[i] = (centerPoint - shadowPositions[i]).SafeNormalize(Vector2.Zero);
            }

            //本体移动到第一个分身位置
            Vector2 mainPos = shadowPositions[0];
            npc.Center = Vector2.Lerp(npc.Center, mainPos, progress * 0.15f);

            //设置蓄力状态
            context.SetChargeState(8, 0.3f + progress * 0.3f);

            //分身生成特效
            if (!VaultUtils.isServer) {
                int showCount = (int)(progress * ShadowCount) + 1;
                showCount = Math.Min(showCount, ShadowCount);

                for (int i = 0; i < showCount; i++) {
                    if (phaseTimer % 3 == 0) {
                        Vector2 pos = shadowPositions[i];
                        //分身位置粒子
                        for (int j = 0; j < 3; j++) {
                            Dust dust = Dust.NewDustDirect(pos + Main.rand.NextVector2Circular(20, 20), 1, 1, DustID.SolarFlare, 0, 0, 150, default, 1.2f);
                            dust.noGravity = true;
                            dust.velocity = Main.rand.NextVector2Circular(2, 2);
                        }
                    }
                }

                //分身出现音效
                if (phaseTimer == 1) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.2f }, npc.Center);
                }
            }
        }

        /// <summary>
        /// 蓄力阶段
        /// </summary>
        private void ExecuteChargePhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase - SplitPhase;
            float progress = phaseTimer / (float)ChargePhase;

            //锁定位置
            npc.Center = shadowPositions[0];
            npc.velocity = Vector2.Zero;

            //面向中心
            npc.rotation = shadowDirections[0].ToRotation() - MathHelper.PiOver2;

            //设置蓄力状态
            context.SetChargeState(8, 0.6f + progress * 0.4f);

            //所有分身蓄力特效
            if (!VaultUtils.isServer) {
                for (int i = 0; i < ShadowCount; i++) {
                    Vector2 pos = shadowPositions[i];
                    Vector2 dir = shadowDirections[i];

                    //能量聚集
                    if (phaseTimer % 2 == 0) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = 50f - progress * 30f;
                        Vector2 dustPos = pos + angle.ToRotationVector2() * dist;
                        Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.SolarFlare, 0, 0, 100, default, 1.4f);
                        dust.noGravity = true;
                        dust.velocity = (pos - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                    }

                    //冲刺预警线
                    if (phaseTimer % 3 == 0 && progress > 0.3f) {
                        float lineDist = 30f + (progress - 0.3f) / 0.7f * 150f;
                        Vector2 linePos = pos + dir * lineDist;
                        Dust dust = Dust.NewDustDirect(linePos, 1, 1, DustID.Torch, 0, 0, 100, default, 1.3f);
                        dust.noGravity = true;
                        dust.velocity = dir * 3f;
                    }
                }

                //蓄力完成闪光
                if (phaseTimer == ChargePhase - 3) {
                    for (int i = 0; i < ShadowCount; i++) {
                        Vector2 pos = shadowPositions[i];
                        for (int j = 0; j < 10; j++) {
                            float angle = MathHelper.TwoPi / 10f * j;
                            Vector2 vel = angle.ToRotationVector2() * 5f;
                            Dust dust = Dust.NewDustDirect(pos, 1, 1, DustID.SolarFlare, vel.X, vel.Y, 0, default, 2f);
                            dust.noGravity = true;
                        }
                    }
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f }, npc.Center);
                }
            }
        }

        /// <summary>
        /// 冲刺阶段
        /// </summary>
        private void ExecuteDashPhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase - SplitPhase - ChargePhase;
            float progress = phaseTimer / (float)DashPhase;

            //停止蓄力特效
            context.ResetChargeState();

            //本体冲刺
            if (!hasDashed) {
                hasDashed = true;
                npc.velocity = shadowDirections[0] * dashSpeed;

                //所有分身发射火球
                if (!VaultUtils.isClient) {
                    for (int i = 1; i < ShadowCount; i++) {
                        Vector2 pos = shadowPositions[i];
                        Vector2 dir = shadowDirections[i];

                        //发射多发火球模拟分身冲刺
                        for (int j = 0; j < 5; j++) {
                            float delay = j * 0.15f;
                            Vector2 shootVel = dir * (dashSpeed - j * 2f);
                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                pos + dir * (j * 30f),
                                shootVel,
                                ModContent.ProjectileType<Fireball>(),
                                35,
                                0f,
                                Main.myPlayer
                            );
                        }
                    }
                }

                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.2f, Volume = 1.2f }, npc.Center);
            }

            //本体朝向速度方向
            FaceVelocity(npc);

            //逐渐减速
            if (phaseTimer > DashPhase / 2) {
                npc.velocity *= 0.96f;
            }

            //分身冲刺轨迹特效
            if (!VaultUtils.isServer && phaseTimer % 2 == 0) {
                for (int i = 0; i < ShadowCount; i++) {
                    Vector2 pos = shadowPositions[i] + shadowDirections[i] * (phaseTimer * dashSpeed * 0.8f);
                    Vector2 dir = shadowDirections[i];

                    //火焰轨迹
                    for (int j = 0; j < 3; j++) {
                        Vector2 dustPos = pos + Main.rand.NextVector2Circular(15, 15);
                        Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.SolarFlare, -dir.X * 3, -dir.Y * 3, 100, default, 1.5f);
                        dust.noGravity = true;
                    }
                }
            }
        }

        /// <summary>
        /// 恢复阶段
        /// </summary>
        private void ExecuteRecoveryPhase(NPC npc, Player player) {
            //逐渐恢复
            npc.velocity *= 0.92f;
            FaceTarget(npc, player.Center);

            //残余火焰粒子
            if (!VaultUtils.isServer && Timer % 4 == 0) {
                Dust dust = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(20, 20), 1, 1, DustID.SolarFlare, 0, -2, 100, default, 0.9f);
                dust.noGravity = true;
            }
        }

        private TwinsStateContext context => Context;
    }
}
