using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 棱彩环阵：以她为心逐层绽出旋转弹环，缺口按黄金角进动；
    /// 图案即威胁，读环、找缺口、穿行
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.PrismRings, typeof(EmpressStateContext))]
    internal class EmpressPrismRingsState : EmpressStateBase
    {
        public override string StateName => "EmpressPrismRings";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.PrismRings;

        private int RingCount => Context.IsSecondPhase ? 5 : 4;
        private int RingInterval => Context.Scaled(46);
        private int TailTime => Context.Scaled(70);
        private int TotalTime => RingInterval * RingCount + TailTime;
        private int BoltsPerRing => Context.IsSecondPhase ? 36 : 30;

        /// <summary>缺口半角：留出可学习的安全通道</summary>
        private const float GapHalfAngle = 0.46f;
        /// <summary>缺口进动步长（黄金角比例，环环相扣不重叠）</summary>
        private const float GapPrecession = 2.399963f * 0.26f;

        private EmpressStateContext Context;
        private float gapSeed;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
            //缺口种子：权威端掷骰；图案完全由弹幕承载，客户端无需知道种子
            if (!VaultUtils.isClient) {
                gapSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            //缓慢压向玩家上方，让环阵始终罩着战场
            if (target.Alives()) {
                GlideTo(npc, target.Center + new Vector2(0f, -300f) + EmpressMotion.Breathing(0.3f), 0.012f, 0.09f, 9f);
            }

            int ringIdx = Timer / RingInterval;
            int beat = Timer % RingInterval;
            bool casting = ringIdx < RingCount;

            //施法节拍：前10帧双手蓄力，落拍绽环
            if (casting && beat >= RingInterval - 12) {
                context.Pose = EmpressPose.CastBoth;
                context.PoseTimer = 20f;//原版臂帧的"举起"窗口
                float chargeT = (beat - (RingInterval - 12)) / 12f;
                context.SetChargeState(3, chargeT);
                EmpressMotion.HandChargeDust(context.LeftHand, chargeT, context.DayFormBlend);
                EmpressMotion.HandChargeDust(context.RightHand, chargeT, context.DayFormBlend);
            }
            else {
                context.Pose = EmpressPose.Idle;
                context.PoseTimer = 0f;
            }

            //落拍：环分三个子拍旋着"画"出来（生成波前），而非凭空整环出现
            if (casting && (beat == RingInterval - 7 || beat == RingInterval - 4 || beat == RingInterval - 1)) {
                int subBeat = (beat - (RingInterval - 7)) / 3;
                CastRing(context, npc, ringIdx, subBeat);
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);

            if (Timer >= TotalTime) {
                return new EmpressConnectorState();
            }
            return null;
        }

        /// <summary>绽出第 ringIdx 环的第 subBeat 三分之一：两个缺口进动，环内切向微旋</summary>
        private void CastRing(EmpressStateContext context, NPC npc, int ringIdx, int subBeat) {
            if (subBeat == 0) {
                PlayLocal(SoundID.Item164 with { Volume = 0.85f, Pitch = -0.1f + ringIdx * 0.08f }, npc.Center);
                EmpressMotion.Shake(npc.Center, 2.6f, 9);
                //发射后坐：环的质量顶了她一下
                npc.velocity -= npc.velocity.SafeNormalize(Vector2.Zero) * 1.6f;
            }

            if (VaultUtils.isClient) {
                return;
            }

            if (subBeat == 0) {
                EmpressCast.Radiance(npc, npc.Center, 130f, 18, ringIdx / (float)RingCount);
            }

            float gapCenterA = gapSeed + ringIdx * GapPrecession;
            float gapCenterB = gapCenterA + MathHelper.Pi;
            //奇偶环反向微旋，相邻环交错成网
            float spiralRate = (ringIdx % 2 == 0 ? 1f : -1f) * 0.0042f;
            float speed = 5.1f + ringIdx * 0.22f;
            if (context.IsDeathMode) {
                speed += 0.5f;
            }

            for (int i = 0; i < BoltsPerRing; i++) {
                //三个子拍按模3认领弹位：环沿圆周被旋着"画"出来
                if (i % 3 != subBeat) {
                    continue;
                }
                float angle = MathHelper.TwoPi / BoltsPerRing * i + ringIdx * 0.13f;
                //缺口扇区跳过
                if (AngleInGap(angle, gapCenterA) || AngleInGap(angle, gapCenterB)) {
                    continue;
                }
                Vector2 dir = angle.ToRotationVector2();
                float hue = (angle / MathHelper.TwoPi + ringIdx * 0.17f) % 1f;
                EmpressCast.Bolt(npc, npc.Center + dir * 84f, dir * speed, context.BoltDamage, 1, hue, spiralRate);
            }
        }

        private static bool AngleInGap(float angle, float gapCenter) {
            return Math.Abs(MathHelper.WrapAngle(angle - gapCenter)) < GapHalfAngle;
        }
    }
}
