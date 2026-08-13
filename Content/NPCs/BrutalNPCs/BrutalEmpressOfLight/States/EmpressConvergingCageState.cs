using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 收缩笼：弹幕在玩家四周悬空成环，蓄势屏息，而后向心合拢；
    /// 缺口随切向分量进动，穿过弹环中心后压力自解——笼是会呼吸的
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.ConvergingCage, typeof(EmpressStateContext))]
    internal class EmpressConvergingCageState : EmpressStateBase
    {
        public override string StateName => "EmpressConvergingCage";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.ConvergingCage;

        private int CageCount => Context.IsSecondPhase ? 2 : 1;
        /// <summary>两座笼的间隔</summary>
        private int CageInterval => Context.Scaled(52);
        private int TotalTime => Context.Scaled(40) + CageCount * CageInterval + Context.Scaled(190);

        /// <summary>悬滞蓄势帧：玩家的读秒窗</summary>
        private const int HoldTime = 50;
        private const float CageRadius = 590f;
        private const int CageBolts = 46;
        private const float GapHalfAngle = 0.30f;

        private EmpressStateContext Context;

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            //她在笼外缓缓绕行，像在鉴赏自己的作品
            if (target.Alives()) {
                float orbitAngle = Timer * 0.012f;
                Vector2 dest = target.Center + new Vector2((float)Math.Cos(orbitAngle) * 520f, -380f + (float)Math.Sin(orbitAngle * 1.4f) * 60f);
                GlideTo(npc, dest, 0.014f, 0.09f, 15f);
            }

            int approach = Context.Scaled(40);
            int cageIdx = (Timer - approach) / CageInterval;
            int beat = (Timer - approach) % CageInterval;

            if (Timer >= approach && cageIdx < CageCount) {
                if (beat < 18) {
                    //铸笼手势
                    context.Pose = EmpressPose.CastBoth;
                    context.PoseTimer = 20f;
                    context.SetChargeState(3, beat / 18f);
                    EmpressMotion.HandChargeDust(context.LeftHand, beat / 18f, context.DayFormBlend);
                    EmpressMotion.HandChargeDust(context.RightHand, beat / 18f, context.DayFormBlend);
                }
                else {
                    context.Pose = EmpressPose.Idle;
                    context.PoseTimer = 0f;
                }

                if (beat == 17) {
                    CastCage(context, npc, target, cageIdx);
                }
            }
            else {
                context.Pose = EmpressPose.Idle;
                context.PoseTimer = 0f;
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);

            if (Timer >= TotalTime) {
                return new EmpressConnectorState();
            }
            return null;
        }

        /// <summary>铸一座笼：环上悬滞蓄势→整环向心收拢，切向分量让缺口进动</summary>
        private void CastCage(EmpressStateContext context, NPC npc, Player target, int cageIdx) {
            PlayLocal(SoundID.Item163 with { Volume = 1f, Pitch = -0.2f }, npc.Center);
            EmpressMotion.Shake(npc.Center, 3f, 10);

            if (VaultUtils.isClient || !target.Alives()) {
                return;
            }

            //笼心锁在此刻的玩家位置：之后怎么走是玩家的事
            Vector2 center = target.Center;
            EmpressCast.Radiance(npc, center, 200f, 24, 0.5f + cageIdx * 0.25f);

            //权威端掷骰缺口方位；第二座笼反向收拢+缺口对侧
            float gapSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            float chirality = cageIdx % 2 == 0 ? 1f : -1f;
            float radius = CageRadius + cageIdx * 90f;
            float inSpeed = (context.IsDeathMode ? 5.2f : 4.6f) + cageIdx * 0.3f;
            float tangential = 1.35f * chirality;
            int hold = context.Scaled(HoldTime);

            for (int i = 0; i < CageBolts; i++) {
                float angle = MathHelper.TwoPi / CageBolts * i;
                //两个进动缺口
                if (Math.Abs(MathHelper.WrapAngle(angle - gapSeed)) < GapHalfAngle
                    || Math.Abs(MathHelper.WrapAngle(angle - gapSeed - MathHelper.Pi)) < GapHalfAngle) {
                    continue;
                }
                Vector2 pos = center + angle.ToRotationVector2() * radius;
                Vector2 inward = (center - pos).SafeNormalize(Vector2.UnitY);
                Vector2 vel = inward * inSpeed + inward.RotatedBy(MathHelper.PiOver2) * tangential;
                float hue = (angle / MathHelper.TwoPi + cageIdx * 0.4f) % 1f;
                //悬滞蓄释：hold帧的读秒预告
                EmpressCast.Bolt(npc, pos, vel, context.BoltDamage, 2, hue, hold);
            }
        }
    }
}
