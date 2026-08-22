using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 弦月突进：入位→迟滞回吸蓄势→一帧点火横贯，
    /// 冲刺尾迹垂直抛洒双列弹幕成扩张的光墙，她本身就是弹幕源
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.CrescentDash, typeof(EmpressStateContext))]
    internal class EmpressCrescentDashState : EmpressStateBase
    {
        public override string StateName => "EmpressCrescentDash";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.CrescentDash;

        private int DashCount => Context.IsSecondPhase ? 3 : 2;

        //单轮节拍
        private int PositionTime => Context.Scaled(34);
        private const int FreezeTime = 9;
        private const int DashTime = 34;
        private int RecoverTime => Context.Scaled(18);
        private int CycleTime => PositionTime + FreezeTime + DashTime + RecoverTime;
        private int TotalTime => DashCount * CycleTime + Context.Scaled(26);

        private const float DashDistance = 860f;

        private EmpressStateContext Context;
        private int dashDir = 1;
        private bool launched;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
            launched = false;
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            int cycleIdx = Timer / CycleTime;
            int beat = Timer % CycleTime;

            if (cycleIdx >= DashCount) {
                //尾拍：滑翔减速，接触伤归零
                npc.damage = 0;
                npc.velocity *= 0.93f;
                context.Pose = EmpressPose.Idle;
                context.PoseTimer = 0f;
                if (Timer >= TotalTime) {
                    return new EmpressConnectorState();
                }
                return null;
            }

            //冲刺姿态：原版绘制附带彩虹环绕残影，PoseTimer映射原版0..90窗口
            context.Pose = dashDir < 0 ? EmpressPose.DashLeft : EmpressPose.DashRight;
            context.PoseTimer = MathHelper.Clamp(beat / (float)CycleTime * 100f, 0f, 100f);

            if (beat < PositionTime) {
                //入位：飞向玩家侧翼；本拍决定冲向
                npc.damage = 0;
                launched = false;
                if (target.Alives()) {
                    dashDir = target.Center.X > npc.Center.X ? 1 : -1;
                    Vector2 dest = target.Center + new Vector2(-dashDir * DashDistance, -8f);
                    //远离入位点太远时用位移闪现遮罩：各端从同步的玩家位确定性执行，
                    //光尘在各端本地绽出；服务端补一发同步兜底
                    if (beat == 2 && npc.Distance(dest) > 1500f) {
                        EmpressMotion.PrismStep(npc, dest + new Vector2(0f, -60f));
                        if (!VaultUtils.isClient) {
                            npc.netUpdate = true;
                        }
                    }
                    GlideTo(npc, dest, 0.03f, 0.11f, 30f);
                    //末几帧迟滞回吸：pow(t,8)反向蓄势
                    float t = beat / (float)PositionTime;
                    npc.Center += EmpressMotion.ReelBack(new Vector2(-dashDir, 0f), t, 4.6f);
                }
            }
            else if (beat < PositionTime + FreezeTime) {
                //屏息：动作全停，辉光拉满，冲刺前的寂静
                npc.damage = 0;
                npc.velocity *= 0.55f;
                context.SetChargeState(3, (beat - PositionTime) / (float)FreezeTime);
                if (beat == PositionTime + 1) {
                    PlayLocal(SoundID.Item160 with { Volume = 1f }, npc.Center);
                }
            }
            else if (beat < PositionTime + FreezeTime + DashTime) {
                int dashBeat = beat - PositionTime - FreezeTime;
                if (dashBeat == 0 && !launched) {
                    //一帧点火
                    launched = true;
                    float speed = context.IsSecondPhase && cycleIdx == DashCount - 1 ? 68f : 60f;
                    npc.velocity = new Vector2(dashDir * speed, 0f);
                    EmpressMotion.Shake(npc.Center, 5f, 12);
                    if (!VaultUtils.isServer) {
                        EmpressScreenFX.PushPrismPulse(npc.Center, 0.28f, 16);
                    }
                }

                //高速窗开接触伤（判定与可见冲刺同窗）；昼形态与原版一致9999
                if (Math.Abs(npc.velocity.X) >= 20f) {
                    npc.damage = context.DayEmpowered ? 9999 : (int)Math.Round(npc.defDamage * 1.35f);
                }
                else {
                    npc.damage = 0;
                }

                //冲刺尾迹：垂直抛洒弹幕，快慢双速交替，光墙张开成双层帷幕
                if (dashBeat % 2 == 0 && Math.Abs(npc.velocity.X) > 24f && !VaultUtils.isClient) {
                    int shed = dashBeat / 2;
                    float hue = (cycleIdx * 0.31f + shed * 0.034f) % 1f;
                    //确定性微斜：抛洒角随索引摆动，光墙有编织感
                    float tilt = (float)Math.Sin(shed * 0.9f) * 0.16f;
                    float wallSpeed = context.IsDeathMode ? 3.6f : 3.1f;
                    //奇偶拍交替快/慢层：同帷幕两种张开速率，视觉双层
                    if (shed % 2 == 1) {
                        wallSpeed *= 0.68f;
                    }
                    Vector2 up = (-MathHelper.PiOver2 + tilt).ToRotationVector2() * wallSpeed;
                    Vector2 down = (MathHelper.PiOver2 - tilt).ToRotationVector2() * wallSpeed;
                    EmpressCast.Bolt(npc, npc.Center, up, context.BoltDamage, 0, hue);
                    EmpressCast.Bolt(npc, npc.Center, down, context.BoltDamage, 0, hue + 0.5f);
                }

                //冲刺后段减速
                if (dashBeat > DashTime - 12) {
                    npc.velocity *= 0.82f;
                }
            }
            else {
                //回稳
                npc.damage = 0;
                npc.velocity *= 0.9f;
                context.ResetChargeState();
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);
            return null;
        }

        public override void OnExit(EmpressStateContext context) {
            base.OnExit(context);
            context.Npc.damage = 0;
        }
    }
}
