using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common
{
    /// <summary>磁暴链锁合击：电弧相连绕场收缩，骤停反转后对视散开</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.TwinsTetherSweep, typeof(TwinsStateContext))]
    internal class TwinsTetherSweepState : TwinsStateBase
    {
        public override string StateName => "TwinsTetherSweep";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.TwinsTetherSweep;

        private int GatherPhase => Context.IsDeathMode ? 46 : 56;
        private int SweepPhase => Context.IsDeathMode ? 250 : 230;
        private const int GazePhase = 36;
        private const int RecoveryPhase = 20;
        private const int MaxPartnerWait = 120;
        private const int ReversePause = 18;

        private int TotalDuration => GatherPhase + SweepPhase + GazePhase + RecoveryPhase;

        private float MaxOrbitSpeed => Context.IsDeathMode ? 0.034f : 0.028f;
        private static float StartRadius => 1050f;
        private static float EndRadius => 610f;

        private TwinsStateContext Context;
        private int comboStep;
        private int partnerWait;
        private Vector2 orbitCenter;
        private float orbitAngle;
        private float sweepAngleAccum;
        private bool arcSpawned;
        private bool reverseDone;

        public TwinsTetherSweepState() : this(0) {
        }

        public TwinsTetherSweepState(int currentComboStep) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            partnerWait = 0;
            sweepAngleAccum = 0f;
            arcSpawned = false;
            reverseDone = false;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //搭档失效→直接退出合击(电弧弹幕会自行消散)
            NPC partner = TwinsStateContext.GetPartnerNpc(npc.type);
            if (!partner.Alives()) {
                TwinsStateContext.ClearComboSignal();
                return GetExitState();
            }

            Timer++;

            if (Timer <= GatherPhase) {
                ExecuteGatherPhase(npc, player);

                //集合末尾标记就绪，等待双方都集合完成再同拍推进
                if (Timer == GatherPhase) {
                    TwinsStateContext.MarkComboReady(context.IsSpazmatism);
                    if (!TwinsStateContext.BothComboReady && partnerWait < MaxPartnerWait) {
                        Timer--;
                        partnerWait++;
                    }
                }
            }
            else if (Timer <= GatherPhase + SweepPhase) {
                ExecuteSweepPhase(npc, player, partner);
            }
            else if (Timer <= GatherPhase + SweepPhase + GazePhase) {
                ExecuteGazePhase(npc, partner);
            }
            else {
                npc.velocity *= 0.92f;
                FaceTarget(npc, player.Center);
            }

            if (Timer >= TotalDuration) {
                return GetExitState();
            }

            return null;
        }

        /// <summary>集合阶段：双眼各自飞向玩家两侧对峙位(魔焰眼取自身所在侧，激光眼取对侧)</summary>
        private void ExecuteGatherPhase(NPC npc, Player player) {
            float progress = Timer / (float)GatherPhase;

            orbitCenter = player.Center;
            //以"魔焰眼在左"为基准角，激光眼自动取反向，保证两眼始终对径
            float baseAngle = Context.IsSpazmatism ? MathHelper.Pi : 0f;
            orbitAngle = baseAngle;
            Vector2 targetPos = orbitCenter + baseAngle.ToRotationVector2() * StartRadius;

            TwinsMotion.SpringHover(npc, targetPos, 0.02f, 0.1f);
            FaceTarget(npc, player.Center);

            Context.SetChargeState(12, progress);

            if (Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.2f, Volume = 0.9f }, npc.Center);
            }

            //能量内聚预兆
            if (Timer % 3 == 0) {
                TwinsMotion.ChargeGatherFX(npc.Center, Context.IsSpazmatism, progress, 70f);
            }
        }

        /// <summary>扫场阶段：电弧链锁成型，双眼绕玩家旋转收缩，中途骤停反转</summary>
        private void ExecuteSweepPhase(NPC npc, Player player, NPC partner) {
            int phaseTimer = Timer - GatherPhase;
            float progress = phaseTimer / (float)SweepPhase;

            Context.ResetChargeState();

            //由魔焰眼(单侧)生成电弧弹幕，避免双重生成
            if (!arcSpawned) {
                arcSpawned = true;
                if (Context.IsSpazmatism && !VaultUtils.isClient) {
                    int damage = Context.IsDeathMode ? 36 : 30;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                        ModContent.ProjectileType<TwinsTetherArc>(), damage, 0f, Main.myPlayer,
                        npc.whoAmI, partner.whoAmI, SweepPhase + GazePhase);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.1f }, npc.Center);
                }
            }

            //旋转速度曲线:缓起→全速，中点骤停并反转旋向
            int half = SweepPhase / 2;
            float speedScale;
            int distFromReverse = Math.Abs(phaseTimer - half);
            if (distFromReverse < ReversePause / 2) {
                //反转骤停窗口
                speedScale = 0f;
                if (!reverseDone && phaseTimer >= half) {
                    reverseDone = true;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item93 with { Pitch = 0.3f, Volume = 1.1f }, npc.Center);
                        TwinsMotion.Shake(player.Center, 5f, 10);
                    }
                }
            }
            else {
                //距离起点/终点/反转点越近转速越低，三段平滑加减速
                int distFromEdge = Math.Min(phaseTimer, SweepPhase - phaseTimer);
                int rampDist = Math.Min(distFromEdge, distFromReverse - ReversePause / 2);
                float ramp = MathHelper.Clamp(rampDist / 40f, 0f, 1f);
                speedScale = VaultUtils.EaseInOutQuad(ramp);
            }

            float direction = phaseTimer < half ? 1f : -1f;
            sweepAngleAccum += MaxOrbitSpeed * speedScale * direction;

            //轨道中心缓慢追随玩家，半径随进度收缩
            orbitCenter = Vector2.Lerp(orbitCenter, player.Center, 0.012f);
            float radius = MathHelper.Lerp(StartRadius, EndRadius, VaultUtils.EaseInOutQuad(progress));

            float baseAngle = Context.IsSpazmatism ? MathHelper.Pi : 0f;
            float myAngle = baseAngle + sweepAngleAccum;
            Vector2 orbitPos = orbitCenter + myAngle.ToRotationVector2() * radius;

            //强弹簧锁轨，保持两眼对径
            TwinsMotion.SpringHover(npc, orbitPos, 0.06f, 0.22f, 40f);

            //面向旋转切线方向，强调离心姿态
            Vector2 tangent = (myAngle + MathHelper.PiOver2 * direction).ToRotationVector2();
            npc.rotation = npc.rotation.AngleLerp(tangent.ToRotation() - MathHelper.PiOver2, 0.2f);
            Context.PushDashVisuals(0.5f * speedScale, 0.6f * speedScale);
        }

        /// <summary>对视阶段：双眼骤停相互凝视，电弧余韵消散(演出小动作)</summary>
        private void ExecuteGazePhase(NPC npc, NPC partner) {
            npc.velocity *= 0.82f;
            //凝视搭档
            float targetRot = (partner.Center - npc.Center).ToRotation() - MathHelper.PiOver2;
            npc.rotation = npc.rotation.AngleLerp(targetRot, 0.25f);

            int phaseTimer = Timer - GatherPhase - SweepPhase;
            if (phaseTimer == 6 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.5f, Volume = 0.6f }, npc.Center);
            }
        }

        /// <summary>退出状态：返回各自二阶段锚点</summary>
        private ITwinsState GetExitState() {
            if (Context.IsSpazmatism) {
                return new SpazmatismFlameChaseState(comboStep);
            }
            return new RetinazerVerticalBarrageState(comboStep);
        }

        public override void OnExit(TwinsStateContext context) {
            base.OnExit(context);
            TwinsStateContext.ClearComboSignal();
        }
    }
}
