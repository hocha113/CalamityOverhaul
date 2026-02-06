using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>
    /// 探针镭射阵状态：毁灭者咆哮后释放探针组成阵列，发射预判线镭射
    /// 阶段1 (0~60帧): 咆哮+体节释放探针，毁灭者减速悬浮
    /// 阶段2 (60~140帧): 探针飞向阵列位置（以目标为中心的圆环/十字），毁灭者缓慢盘旋
    /// 阶段3 (140~200帧): 探针锁定目标，发射PrimeCannonOnSpan预判线
    /// 阶段4 (200~260帧): 恢复期，探针散开消失，切换状态
    /// </summary>
    internal class DestroyerProbeMatrixState : DestroyerStateBase
    {
        public override string StateName => "ProbeMatrix";

        private const int RoarPhase = 60;
        private const int FormationPhase = 140;
        private const int FirePhase = 200;
        private const int RecoveryPhase = 260;

        private int probeCount;
        private int[] probeIndices;
        private bool probesSpawned;
        private bool probesFired;
        private int formationType; // 0=圆环 1=十字 2=V字

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            probesSpawned = false;
            probesFired = false;

            probeCount = context.IsEnraged ? 12 : 8;
            if (context.IsDeathMode) probeCount += 4;

            probeIndices = new int[probeCount];
            formationType = Main.rand.Next(3);

            context.SetChargeState(4, 0f);
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //阶段1：咆哮+释放探针
            if (Timer <= RoarPhase) {
                ExecuteRoarPhase(context);
            }
            //阶段2：探针飞向阵列位置
            else if (Timer <= FormationPhase) {
                ExecuteFormationPhase(context);
            }
            //阶段3：探针发射镭射
            else if (Timer <= FirePhase) {
                ExecuteFirePhase(context);
            }
            //阶段4：恢复
            else if (Timer <= RecoveryPhase) {
                ExecuteRecoveryPhase(context);
            }
            else {
                return new DestroyerPatrolState();
            }

            //全程缓慢盘旋
            float patrolTime = Timer * 0.01f;
            Vector2 offset = new Vector2(
                (float)Math.Cos(patrolTime) * 600f,
                (float)Math.Sin(patrolTime) * 300f - 400f);
            SetMovement(context, player.Center + offset, 14f, 0.4f);

            return null;
        }

        private void ExecuteRoarPhase(DestroyerStateContext context) {
            NPC npc = context.Npc;

            context.SetChargeState(4, Timer / (float)RoarPhase * 0.3f);

            //咆哮音效
            if (Timer == 1) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.2f }, npc.Center);
            }

            //减速
            npc.velocity *= 0.95f;

            //从体节释放探针
            if (!probesSpawned && !VaultUtils.isClient && Timer >= 20) {
                SpawnProbes(context);
                probesSpawned = true;
                npc.netUpdate = true;
            }
        }

        private void SpawnProbes(DestroyerStateContext context) {
            var segments = context.BodySegments;
            if (segments.Count == 0) return;

            for (int i = 0; i < probeCount; i++) {
                //从均匀分布的体节位置释放
                int segIdx = (int)((float)i / probeCount * segments.Count);
                segIdx = Math.Clamp(segIdx, 0, segments.Count - 1);
                NPC sourceSegment = segments[segIdx];

                if (!sourceSegment.active) continue;

                int probeIdx = NPC.NewNPC(sourceSegment.GetSource_FromAI(),
                    (int)sourceSegment.Center.X, (int)sourceSegment.Center.Y, NPCID.Probe);

                if (probeIdx >= 0 && probeIdx < Main.maxNPCs) {
                    probeIndices[i] = probeIdx;
                    NPC probe = Main.npc[probeIdx];
                    //标记为阵列探针，ProbeAI会检测这个标记
                    probe.ai[3] = -1f;
                    probe.velocity = Main.rand.NextVector2Circular(4f, 4f);
                    probe.netUpdate = true;
                }
                else {
                    probeIndices[i] = -1;
                }
            }
        }

        private void ExecuteFormationPhase(DestroyerStateContext context) {
            Player player = context.Target;
            float formProgress = (Timer - RoarPhase) / (float)(FormationPhase - RoarPhase);
            context.SetChargeState(4, 0.3f + formProgress * 0.4f);

            //引导探针飞向目标位置
            for (int i = 0; i < probeCount; i++) {
                if (probeIndices[i] < 0) continue;
                NPC probe = Main.npc[probeIndices[i]];
                if (!probe.active || probe.type != NPCID.Probe) continue;

                Vector2 targetPos = GetFormationPosition(player.Center, i, probeCount, formationType);

                //平滑飞向目标
                Vector2 toTarget = targetPos - probe.Center;
                float dist = toTarget.Length();
                float flySpeed = MathHelper.Lerp(2f, 18f, formProgress);
                probe.velocity = Vector2.Lerp(probe.velocity, toTarget.SafeNormalize(Vector2.Zero) * Math.Min(dist * 0.1f, flySpeed), 0.15f);
                probe.rotation = (player.Center - probe.Center).ToRotation();

                //禁用探针自身AI
                probe.ai[3] = -1f;

                //到位后减速
                if (dist < 30f) {
                    probe.velocity *= 0.8f;
                }
            }
        }

        private void ExecuteFirePhase(DestroyerStateContext context) {
            Player player = context.Target;
            context.SetChargeState(4, 0.7f + (Timer - FormationPhase) / (float)(FirePhase - FormationPhase) * 0.3f);

            //探针锁定并发射
            if (!probesFired && Timer == FormationPhase + 10) {
                probesFired = true;

                if (!VaultUtils.isClient) {
                    int projType = ModContent.ProjectileType<PrimeCannonOnSpan>();
                    int damage = HeadPrimeAI.SetMultiplier(CWRRef.GetProjectileDamage(context.Npc, projType));

                    for (int i = 0; i < probeCount; i++) {
                        if (probeIndices[i] < 0) continue;
                        NPC probe = Main.npc[probeIndices[i]];
                        if (!probe.active || probe.type != NPCID.Probe) continue;

                        Vector2 dir = (player.Center - probe.Center).SafeNormalize(Vector2.UnitY);
                        SoundEngine.PlaySound(SoundID.Item12, probe.Center);

                        Projectile.NewProjectile(probe.GetSource_FromAI(),
                            probe.Center, dir, projType, damage, 0f,
                            Main.myPlayer, probe.whoAmI, player.whoAmI, 0);
                    }
                }
            }

            //探针保持位置，轻微悬浮
            for (int i = 0; i < probeCount; i++) {
                if (probeIndices[i] < 0) continue;
                NPC probe = Main.npc[probeIndices[i]];
                if (!probe.active || probe.type != NPCID.Probe) continue;

                probe.velocity *= 0.9f;
                probe.ai[3] = -1f;
                probe.rotation = (player.Center - probe.Center).ToRotation();
            }
        }

        private void ExecuteRecoveryPhase(DestroyerStateContext context) {
            context.ResetChargeState();

            //攻击完成后快速消灭探针，避免滞留造成混乱
            KillAllProbes();
        }

        /// <summary>
        /// 计算探针在阵列中的位置
        /// </summary>
        private static Vector2 GetFormationPosition(Vector2 center, int index, int total, int type) {
            return type switch {
                0 => GetCirclePosition(center, index, total),
                1 => GetCrossPosition(center, index, total),
                _ => GetVFormationPosition(center, index, total)
            };
        }

        private static Vector2 GetCirclePosition(Vector2 center, int index, int total) {
            float angle = MathHelper.TwoPi / total * index;
            return center + angle.ToRotationVector2() * 350f;
        }

        private static Vector2 GetCrossPosition(Vector2 center, int index, int total) {
            int arm = index % 4;
            int posInArm = index / 4;
            float spacing = 120f;
            float offset = (posInArm + 1) * spacing;

            return arm switch {
                0 => center + new Vector2(offset, 0),
                1 => center + new Vector2(-offset, 0),
                2 => center + new Vector2(0, offset),
                _ => center + new Vector2(0, -offset)
            };
        }

        private static Vector2 GetVFormationPosition(Vector2 center, int index, int total) {
            bool leftSide = index % 2 == 0;
            int row = index / 2;
            float spacing = 100f;
            float spreadAngle = MathHelper.ToRadians(30);

            Vector2 baseOffset = new Vector2(0, -1).RotatedBy(leftSide ? -spreadAngle : spreadAngle);
            return center + baseOffset * (row + 1) * spacing + new Vector2(0, 200);
        }

        /// <summary>
        /// 消灭所有阵列探针
        /// </summary>
        private void KillAllProbes() {
            for (int i = 0; i < probeCount; i++) {
                if (probeIndices[i] < 0) continue;
                NPC probe = Main.npc[probeIndices[i]];
                if (!probe.active || probe.type != NPCID.Probe) continue;

                probe.life = 0;
                probe.HitEffect();
                probe.active = false;
                probe.netUpdate = true;
            }
        }

        public override void OnExit(DestroyerStateContext context) {
            base.OnExit(context);
            context.ResetChargeState();
            KillAllProbes();
        }
    }
}
