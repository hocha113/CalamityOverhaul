using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 聚集之光：60f 就位充能（世界为它变暗）→ 释放帧冲击波推开 → 120f 八臂加速光球螺旋。
    /// 先把人推远，再开火，是这招的公平阀
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.LightSpiral, typeof(EmpressStateContext))]
    internal class EmpressLightSpiralState : EmpressStateBase
    {
        public override string StateName => "EmpressLightSpiral";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.LightSpiral;

        /// <summary>充能至少一小节，在第一拍释放；开火两小节；收势 40f</summary>
        private const int ChargeTime = EmpressTempo.BarFrames;
        private const int FireTime = EmpressTempo.BarFrames * 2;
        private const int RecoverTime = 40;

        private EmpressStateContext Context;
        /// <summary>释放后的帧计数，-1=还在充能等第一拍</summary>
        private int fireTick = -1;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
            fireTick = -1;
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            if (fireTick < 0) {
                ChargeUpdate(context, npc, target);
                if (Timer >= ChargeTime && context.Downbeat) {
                    Release(context, npc);
                    fireTick = 0;
                }
            }
            else if (fireTick < FireTime) {
                fireTick++;
                FireUpdate(context, npc, fireTick);
            }
            else {
                fireTick++;
                context.Pose = EmpressPose.Idle;
                context.PoseTimer = 0f;
                npc.velocity *= 0.92f;
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);
            if (fireTick >= FireTime + RecoverTime) {
                return new EmpressConnectorState();
            }
            return null;
        }

        private void ChargeUpdate(EmpressStateContext context, NPC npc, Player target) {
            float t = MathHelper.Clamp(Timer / (float)ChargeTime, 0f, 1f);
            context.Pose = EmpressPose.CastBoth;
            context.PoseTimer = MathHelper.Clamp(Timer * 1.5f, 0f, 60f);
            context.SetChargeState(3, t);

            if (target.Alives()) {
                bool right = target.Center.X > npc.Center.X;
                Vector2 dest = target.Center + new Vector2(right ? -250f : 250f, -330f);
                //就位越到后段越紧（sqrt 爬升的弹簧刚度）
                float k = 0.0233f * MathF.Sqrt(t);
                npc.velocity = npc.velocity * 0.8f + (dest - npc.Center) * k;
            }
            else {
                npc.velocity *= 0.9f;
            }

            if (!VaultUtils.isServer) {
                if (context.DayEmpowered) {
                    //世界为这一招变暗
                    EmpressDayDrive.AddBlackout(0.011f);
                }
                EmpressMotion.HandChargeDust(context.CastHand, t, context.DayFormBlend);
                if (Timer % 8 == 0) {
                    EmpressMotion.Shake(npc.Center, t * 1.2f, 6);
                }
            }
        }

        /// <summary>释放帧：冲击波推开、震屏、双音，之后才开火</summary>
        private void Release(EmpressStateContext context, NPC npc) {
            npc.velocity *= 0.5f;
            EmpressCast.Shockwave(npc, context.CastHand + npc.velocity);
            PlayLocal(SoundID.Item165 with { Volume = 1.2f, Pitch = -0.2f }, npc.Center);
            PlayLocal(SoundID.Item122 with { Volume = 0.9f, Pitch = -0.1f }, npc.Center);
            EmpressMotion.Shake(npc.Center, 12f, 18);
            if (!VaultUtils.isServer) {
                EmpressScreenFX.PushPrismPulse(context.CastHand, 0.45f, 24);
                EmpressMotion.SparkBurst(context.CastHand, Vector2.UnitY, 26, 4f, 12f, context.DayFormBlend, MathHelper.Pi);
            }
        }

        private void FireUpdate(EmpressStateContext context, NPC npc, int fireTick) {
            context.Pose = EmpressPose.CastRight;
            context.PoseTimer = 30f + (fireTick % 20);
            npc.velocity *= 0.9f;

            bool day = context.DayEmpowered;
            //昼 4f 八臂（同屏约 240 球，给弹幕表留余量）；夜 5~6f 六臂
            int interval = day ? 4 : (context.IsSecondPhase ? 5 : 6);
            int arms = day ? 8 : 6;
            if (fireTick % interval != 0 || VaultUtils.isClient) {
                return;
            }
            int volley = fireTick / interval;
            //八臂螺旋：每轮转 6.427°，再叠一层慢漂移让臂微弯
            float turn = MathHelper.ToRadians(6.427f) * volley + volley / (360f / arms / 6.427f) * MathHelper.ToRadians(3f);
            for (int j = 0; j < arms; j++) {
                float angle = MathHelper.TwoPi / arms * j + turn;
                Vector2 dir = angle.ToRotationVector2();
                EmpressCast.Bolt(npc, context.CastHand, dir * (day ? 16f : 13f), context.BoltDamage, EmpressBoltMode.Accelerating);
            }
            if (volley % 4 == 0) {
                PlayLocal(SoundID.Item164 with { Volume = 0.55f, Pitch = 0.2f + volley * 0.004f }, npc.Center);
            }
        }
    }
}
