using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 冲刺抓取：入位→迟滞回吸→屏息→一帧点火，冲刺期按"玩家速度+朝向×50"转向追击；
    /// 接触窗 |v|≥20，命中且投技冷却到则接光绫缚舞。冲刺尾迹垂直抛洒双列光球
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.DashGrab, typeof(EmpressStateContext))]
    internal class EmpressDashGrabState : EmpressStateBase
    {
        public override string StateName => "EmpressDashGrab";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.DashGrab;

        private int DashCount => Context.IsSecondPhase ? 3 : 2;
        private int PositionTime => Context.Scaled(34);
        private const int FreezeTime = 9;
        private const int DashTime = 40;
        private int RecoverTime => Context.Scaled(18);
        private int CycleTime => PositionTime + FreezeTime + DashTime + RecoverTime;
        private int TotalTime => DashCount * CycleTime + Context.Scaled(20);

        private const float StandoffDistance = 700f;
        private const float LaunchSpeed = 40f;
        private const float MaxSpeed = 50f;

        private EmpressStateContext Context;
        private int dashDir = 1;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            int cycleIdx = Timer / CycleTime;
            int beat = Timer % CycleTime;

            if (cycleIdx >= DashCount) {
                npc.damage = 0;
                npc.velocity *= 0.93f;
                context.Pose = EmpressPose.Idle;
                context.PoseTimer = 0f;
                return Timer >= TotalTime ? new EmpressConnectorState() : null;
            }

            //冲刺姿态：原版绘制附带彩虹环绕残影，PoseTimer 映射原版 0..90 窗口
            context.Pose = dashDir < 0 ? EmpressPose.DashLeft : EmpressPose.DashRight;
            context.PoseTimer = MathHelper.Clamp(beat / (float)CycleTime * 90f, 0f, 90f);

            if (beat < PositionTime) {
                npc.damage = 0;
                if (target.Alives()) {
                    dashDir = target.Center.X > npc.Center.X ? 1 : -1;
                    Vector2 dest = target.Center + new Vector2(-dashDir * StandoffDistance, -40f);
                    if (beat == 2 && npc.Distance(dest) > 1500f) {
                        EmpressMotion.PrismStep(npc, dest + new Vector2(0f, -60f), context.DayFormBlend);
                        if (!VaultUtils.isClient) {
                            npc.netUpdate = true;
                        }
                    }
                    GlideTo(npc, dest, 0.03f, 0.11f, 30f);
                    //末几帧迟滞回吸：pow(t,8) 反向蓄势
                    float t = beat / (float)PositionTime;
                    npc.Center += EmpressMotion.ReelBack(new Vector2(-dashDir, 0f), t, 4.6f);
                }
            }
            else if (beat < PositionTime + FreezeTime) {
                //屏息：动作全停，辉光拉满
                npc.damage = 0;
                npc.velocity *= 0.55f;
                context.SetChargeState(3, (beat - PositionTime) / (float)FreezeTime);
                if (beat == PositionTime + 1) {
                    PlayLocal(SoundID.Item160 with { Volume = 1f }, npc.Center);
                }
            }
            else if (beat < PositionTime + FreezeTime + DashTime) {
                int dashBeat = beat - PositionTime - FreezeTime;
                IEmpressState grab = DashUpdate(context, npc, target, dashBeat, cycleIdx);
                if (grab != null) {
                    return grab;
                }
            }
            else {
                npc.damage = 0;
                npc.velocity *= 0.9f;
                context.ResetChargeState();
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);
            return null;
        }

        private IEmpressState DashUpdate(EmpressStateContext context, NPC npc, Player target, int dashBeat, int cycleIdx) {
            bool day = context.DayEmpowered;
            if (dashBeat == 0) {
                //一帧点火：朝 33f 预测点
                Vector2 aim = target.Alives() ? target.Center + target.velocity * 33f : npc.Center + new Vector2(dashDir, 0f);
                dashDir = aim.X > npc.Center.X ? 1 : -1;
                npc.velocity = npc.DirectionTo(aim).SafeNormalize(new Vector2(dashDir, 0f)) * LaunchSpeed;
                EmpressMotion.ShakeAlong(npc.Center, npc.velocity, 6f, 12);
                PlayLocal(SoundID.Item160 with { Volume = 0.9f, Pitch = 0.3f }, npc.Center);
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.PushPrismPulse(npc.Center, 0.28f, 16);
                }
            }
            else if (target.Alives() && npc.Distance(target.Center) > 80f && dashBeat < DashTime - 10) {
                //转向追击：加速度 2.75（昼二阶段 3.75）
                float accel = day ? (context.IsSecondPhase ? 3.75f : 3f) : 2.5f;
                EmpressMotion.Pursue(npc, target, accel);
                if (npc.velocity.Length() > MaxSpeed) {
                    npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * MaxSpeed;
                }
            }

            //接触窗与可见冲刺同窗
            bool hot = npc.velocity.Length() >= 20f;
            npc.damage = hot ? (int)Math.Round(npc.defDamage * (day ? 1.5f : 1.35f)) : 0;

            if (!VaultUtils.isServer && hot) {
                EmpressScreenFX.PushFlash(npc.velocity, 0.006f * npc.velocity.Length() * (1f - Math.Min(npc.Distance(Main.LocalPlayer.Center) / 900f, 1f)));
            }

            //命中即抓：服务端用碰撞盒相交判定，冷却到则接投技
            if (!VaultUtils.isClient && hot && context.GrabCooldown <= 0 && target.Alives()) {
                Rectangle reach = npc.Hitbox;
                reach.Inflate(12, 12);
                if (reach.Intersects(target.Hitbox)) {
                    npc.target = target.whoAmI;
                    npc.netUpdate = true;
                    context.GrabCooldown = EmpressLightBindWaltzState.GrabCooldownTicks;
                    return new EmpressLightBindWaltzState();
                }
            }

            //尾迹：垂直抛洒双列光球，快慢层交替张成双层帷幕
            if (dashBeat % 4 == 0 && hot && !VaultUtils.isClient) {
                int shed = dashBeat / 4;
                float tilt = (float)Math.Sin(shed * 0.9f) * 0.16f;
                float wallSpeed = (day ? 3.4f : 3f) * (shed % 2 == 1 ? 0.68f : 1f);
                Vector2 up = (-MathHelper.PiOver2 + tilt).ToRotationVector2() * wallSpeed;
                Vector2 down = (MathHelper.PiOver2 - tilt).ToRotationVector2() * wallSpeed;
                EmpressCast.Bolt(npc, npc.Center, up, context.BoltDamage, EmpressBoltMode.Straight);
                EmpressCast.Bolt(npc, npc.Center, down, context.BoltDamage, EmpressBoltMode.Straight);
            }

            //冲刺后段硬刹
            if (dashBeat > DashTime - 10) {
                npc.velocity *= 0.8f;
            }
            return null;
        }

        public override void OnExit(EmpressStateContext context) {
            base.OnExit(context);
            context.Npc.damage = 0;
        }
    }
}
