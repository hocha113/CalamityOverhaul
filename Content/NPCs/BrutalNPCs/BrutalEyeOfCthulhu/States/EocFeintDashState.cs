using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.States
{
    /// <summary>
    /// 变轨假动作冲刺：后撤蓄力→直线暴冲→中途苍白瞬闪预告→猛拐变轨，谎言残影沿旧轨道续飞<br/>
    /// 变轨帧由权威端掷骰写入 npc.ai[3] 同步，瞬闪是全端一致的公平前摇
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EocStateIndex.FeintDash, typeof(EocStateContext))]
    internal class EocFeintDashState : EocStateBase
    {
        public override string StateName => "EocFeintDash";
        public override EocStateIndex StateIndex => EocStateIndex.FeintDash;

        private enum DashPhase
        {
            Track,      //绕侧接近
            Reel,       //后撤蓄力
            Flight,     //冲刺飞行(含变轨)
            Brake,      //硬刹
        }

        private const int TrackTime = 20;
        private const int FlightTime = 27;
        private const int BrakeTime = 13;
        /// <summary>瞬闪提前量：变轨前几帧发出苍白闪</summary>
        private const int BlinkLead = 5;

        private int ReelTime => Context.IsAsuraMode ? 22 : 27;
        private int MaxDashes => Context.IsAsuraMode ? 4 : 3;
        private float DashSpeed => (Context.IsAsuraMode ? 50f : 44f) + (Context.IsLowPhase ? 3f : 0f);
        private float ContactMult => Context.IsSecondPhase ? 1.3f : 1.1f;

        private EocStateContext Context;
        private DashPhase phase;
        private int dashCount;
        private Vector2 flankPoint;
        private bool kinked;
        private bool kinked2;

        public override void OnEnter(EocStateContext context) {
            base.OnEnter(context);
            Context = context;
            phase = DashPhase.Track;
            dashCount = 0;
            kinked = kinked2 = false;
        }

        public override IEocState OnUpdate(EocStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            DisableContactDamage(npc);

            switch (phase) {
                case DashPhase.Track:
                    UpdateTrack(npc, player);
                    break;
                case DashPhase.Reel:
                    UpdateReel(npc, player, context);
                    break;
                case DashPhase.Flight:
                    UpdateFlight(npc, player, context);
                    break;
                case DashPhase.Brake:
                    UpdateBrake(npc, context);
                    break;
            }

            //收招决策仅权威端
            if (phase == DashPhase.Brake && Timer >= BrakeTime && dashCount >= MaxDashes) {
                if (VaultUtils.isClient) {
                    return null;
                }
                return new EocVeilHoverState(context.IsAsuraMode ? 40 : 56);
            }

            return null;
        }

        private void SwitchPhase(DashPhase next) {
            phase = next;
            Timer = 0;
        }

        private void UpdateTrack(NPC npc, Player player) {
            if (Timer == 0) {
                float side = npc.Center.X < player.Center.X ? -1f : 1f;
                flankPoint = player.Center + new Vector2(side * 430f, -60f);
            }
            flankPoint += player.velocity * 0.4f;
            EocMotion.CurveChase(npc, flankPoint, 21f, 0.11f);
            FaceTarget(npc, player.Center, 0.3f);

            Timer++;
            if (Timer >= TrackTime || npc.Distance(flankPoint) < 60f) {
                SwitchPhase(DashPhase.Reel);
            }
        }

        private void UpdateReel(NPC npc, Player player, EocStateContext context) {
            float progress = Timer / (float)ReelTime;
            Vector2 awayDir = (npc.Center - player.Center).SafeNormalize(Vector2.UnitY);
            EocMotion.ReelBack(npc, awayDir, progress, 5f);
            FaceTarget(npc, player.Center, 0.5f);
            context.SetChargeState(1, progress);
            context.PushIris(progress, EocMotion.IrisRed);

            //末段绷紧颤抖
            if (progress > 0.72f && !VaultUtils.isServer) {
                npc.position += Main.rand.NextVector2Circular(1.7f, 1.7f);
            }

            //车道预警随蓄力显形
            Vector2 aimDir = (EocMotion.PredictTarget(player, npc.Center, DashSpeed, 0.55f) - npc.Center)
                .SafeNormalize(Vector2.UnitY);
            context.LaneIntensity = 0.4f + progress * 0.6f;
            context.LaneStart = npc.Center;
            context.LaneDir = aimDir;
            context.LaneLength = 1350f;
            context.LaneProgress = progress;

            //内聚血丝
            if (Timer % 2 == 0) {
                EocMotion.ConvergeStreaks(npc.Center, progress, 130f);
            }
            //蓄力起手音（固定提前量，可被玩家内化）
            if (Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.8f, Pitch = -0.45f }, npc.Center);
            }

            Timer++;
            if (Timer >= ReelTime) {
                //起跑：权威端掷变轨帧写 ai[3]，随 netUpdate 下发
                if (!VaultUtils.isClient) {
                    npc.ai[3] = Main.rand.Next(8, 14);
                    Vector2 predicted = EocMotion.PredictTarget(player, npc.Center, DashSpeed, 0.55f);
                    Vector2 dir = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);
                    EocMotion.DashLaunch(npc, context, dir, DashSpeed);
                    npc.netUpdate = true;
                }
                else {
                    //客户端本地演出即时反馈，轨迹以服务器包为准
                    EocMotion.DashLaunch(npc, context, (player.Center - npc.Center).SafeNormalize(Vector2.UnitY), DashSpeed);
                }
                context.ResetChargeState();
                kinked = kinked2 = false;
                FaceVelocity(npc);
                SwitchPhase(DashPhase.Flight);
            }
        }

        private void UpdateFlight(NPC npc, Player player, EocStateContext context) {
            context.PushDashVisuals(1f, 1f);
            FaceVelocity(npc);
            EnableContactDamageIfFast(npc, 26f, ContactMult);

            int kinkFrame = Math.Max((int)npc.ai[3], 6);

            //苍白瞬闪：变轨的公平预告，全端按同一 ai[3] 帧触发
            if (Timer == kinkFrame - BlinkLead) {
                EocMotion.FeintBlink(npc, context);
            }

            //变轨
            if (Timer == kinkFrame && !kinked) {
                kinked = true;
                Vector2 oldVel = npc.velocity;
                if (!VaultUtils.isClient) {
                    float currentHeading = npc.velocity.ToRotation();
                    float desired = (player.Center - npc.Center).ToRotation();
                    float newHeading = currentHeading.AngleTowards(desired, MathHelper.ToRadians(75f));
                    npc.velocity = newHeading.ToRotationVector2() * npc.velocity.Length() * 1.14f;
                    npc.netUpdate = true;
                }
                EocMotion.KinkBurst(npc, context, oldVel, context.IsSecondPhase);
            }

            //修罗模式二次小变轨
            if (context.IsAsuraMode && kinked && !kinked2 && Timer == kinkFrame + 9) {
                kinked2 = true;
                Vector2 oldVel = npc.velocity;
                if (!VaultUtils.isClient) {
                    float currentHeading = npc.velocity.ToRotation();
                    float desired = (player.Center - npc.Center).ToRotation();
                    float newHeading = currentHeading.AngleTowards(desired, MathHelper.ToRadians(40f));
                    npc.velocity = newHeading.ToRotationVector2() * npc.velocity.Length() * 1.07f;
                    npc.netUpdate = true;
                }
                EocMotion.KinkBurst(npc, context, oldVel, context.IsSecondPhase);
            }

            Timer++;
            if (Timer >= FlightTime) {
                dashCount++;
                SwitchPhase(DashPhase.Brake);
            }
        }

        private void UpdateBrake(NPC npc, EocStateContext context) {
            npc.velocity *= 0.68f;
            EocMotion.BrakeDroplets(npc);
            EnableContactDamageIfFast(npc, 26f, ContactMult);
            FaceTarget(npc, context.Target.Center, 0.2f);

            Timer++;
            if (Timer >= BrakeTime && dashCount < MaxDashes) {
                SwitchPhase(DashPhase.Track);
            }
        }
    }
}
