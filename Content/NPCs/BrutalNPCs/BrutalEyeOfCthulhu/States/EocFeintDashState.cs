using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.States
{
    /// <summary>
    /// 变轨假动作冲刺：后撤蓄力→擦向玩家侧方直线暴冲→中途苍白瞬闪→猛拐贯穿，谎言残影沿旧轨道续飞<br/>
    /// 公平契约（预告即承诺）：蓄力末段车道冻结，折线整条画出，起跑后按锁定路径飞，不再按玩家实时位置重瞄；
    /// 变轨帧与拐点侧由权威端打包写 npc.ai[3] 同步
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
        /// <summary>蓄力末段车道冻结帧数：锁定后至少这么多帧+起跑段飞行时间可供玩家离线</summary>
        private const int AimLockFrames = 9;
        /// <summary>拐点在玩家正侧方的距离：贯穿段长度，决定变轨后留给玩家的读秒</summary>
        private const float KinkLateral = 300f;
        /// <summary>变轨后提速倍率</summary>
        private const float KinkSpeedMul = 1.14f;

        private int ReelTime => Context.IsAsuraMode ? 22 : 27;
        private int MaxDashes => Context.IsAsuraMode ? 4 : 3;
        private float DashSpeed => (Context.IsAsuraMode ? 50f : 44f) + (Context.IsLowPhase ? 3f : 0f);
        private float ContactMult => Context.IsSecondPhase ? 1.3f : 1.1f;

        private EocStateContext Context;
        private DashPhase phase;
        private int dashCount;
        private Vector2 flankPoint;
        private EocDashPlan plan;
        /// <summary>本段拐点侧，各端在蓄力起始帧由 ai[3] 同构推导</summary>
        private float repSide = 1f;
        private bool aimLocked;
        private bool blinked;
        private bool kinked;
        private bool kinked2;

        public override void OnEnter(EocStateContext context) {
            base.OnEnter(context);
            Context = context;
            phase = DashPhase.Track;
            dashCount = 0;
            aimLocked = false;
            blinked = kinked = kinked2 = false;
            plan = default;
            //首段拐点侧由权威端掷骰，以 ±100 占位写 ai[3]；与状态切换同包下发，客户端入态即知
            if (!VaultUtils.isClient) {
                context.Npc.ai[3] = Main.rand.NextBool() ? 100f : -100f;
                context.Npc.netUpdate = true;
            }
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
                    UpdateFlight(npc, context);
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
            //拐点侧逐段交替：首段读入态时下发的占位符号，之后各端都从上一段的同步值取反，无需等包
            if (Timer == 0) {
                aimLocked = false;
                repSide = dashCount == 0 ? EocDashPlan.SideFromPacked(npc.ai[3]) : -EocDashPlan.SideFromPacked(npc.ai[3]);
                if (!VaultUtils.isClient) {
                    npc.ai[3] = repSide * 100f;
                    npc.netUpdate = true;
                }
            }

            float progress = Timer / (float)ReelTime;
            Vector2 awayDir = (npc.Center - player.Center).SafeNormalize(Vector2.UnitY);
            EocMotion.ReelBack(npc, awayDir, progress, 5f);
            //锁定前盯人，锁定后瞳孔转向承诺的起跑段：眼神也是预告的一部分
            FaceTarget(npc, aimLocked ? npc.Center + plan.Dir1 : player.Center, 0.5f);
            context.SetChargeState(1, progress);
            context.PushIris(progress, EocMotion.IrisRed);

            //末段绷紧颤抖
            if (progress > 0.72f && !VaultUtils.isServer) {
                BossNetMotion.DrawShake(npc, Main.rand.NextVector2Circular(1.7f, 1.7f));
            }

            //承诺路径：未锁定时每帧按当前位置重建，锁定后冻结，只让起点跟着眼体走
            if (!aimLocked) {
                plan = EocDashPlan.Build(npc.Center, player.Center, repSide,
                    DashSpeed, KinkSpeedMul, KinkLateral, FlightTime, context.IsAsuraMode, MathHelper.ToRadians(40f));
                if (Timer >= ReelTime - AimLockFrames) {
                    aimLocked = true;
                    if (!VaultUtils.isClient) {
                        npc.ai[3] = plan.Pack();
                        npc.netUpdate = true;
                    }
                    EocMotion.AimLockCue(npc, context, plan.Dir1);
                }
            }
            plan.WriteLane(context, npc.Center, progress, aimLocked);

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
                //起跑沿锁定的起跑段；客户端用本地同构路径即时演出，轨迹以服务器包为准
                EocMotion.DashLaunch(npc, context, plan.Dir1, DashSpeed);
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                context.ResetChargeState();
                blinked = kinked = kinked2 = false;
                FaceVelocity(npc);
                SwitchPhase(DashPhase.Flight);
            }
        }

        private void UpdateFlight(NPC npc, EocStateContext context) {
            context.PushDashVisuals(1f, 1f);
            FaceVelocity(npc);
            EnableContactDamageIfFast(npc, 26f, ContactMult);

            //变轨帧优先取同步值，包未到则用本地几何值；用 >= 判定，同步值晚到也不会漏掉拐弯
            int kinkFrame = plan.ResolveKinkFrame(npc.ai[3], FlightTime);

            //苍白瞬闪：拐弯前最后一重提醒
            if (!blinked && Timer >= kinkFrame - BlinkLead) {
                blinked = true;
                EocMotion.FeintBlink(npc, context);
            }

            //变轨：沿锁定的贯穿段，不看玩家现在在哪
            if (!kinked && Timer >= kinkFrame) {
                kinked = true;
                Vector2 oldVel = npc.velocity;
                npc.velocity = plan.Dir2 * npc.velocity.Length() * KinkSpeedMul;
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                EocMotion.KinkBurst(npc, context, oldVel, context.IsSecondPhase);
            }

            //修罗模式回钩：穿过玩家后再拐一次，同样画在车道里
            if (plan.Kink2Frame >= 0 && kinked && !kinked2 && Timer >= plan.Kink2Frame) {
                kinked2 = true;
                Vector2 oldVel = npc.velocity;
                npc.velocity = plan.Dir3 * npc.velocity.Length() * 1.07f;
                if (!VaultUtils.isClient) {
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
