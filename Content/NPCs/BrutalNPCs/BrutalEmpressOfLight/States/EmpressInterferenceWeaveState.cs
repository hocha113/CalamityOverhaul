using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 干涉织网：左右手各引一条反向旋臂，双螺旋在空间里交织出莫尔纹
    /// 弹幕图案本身即是美学主体，网眼是可学习的呼吸通道
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.InterferenceWeave, typeof(EmpressStateContext))]
    internal class EmpressInterferenceWeaveState : EmpressStateBase
    {
        public override string StateName => "EmpressInterferenceWeave";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.InterferenceWeave;

        private int WeaveTime => Context.Scaled(168);
        private int TotalTime => WeaveTime + Context.Scaled(80);

        /// <summary>相邻两发的角步长（约9.6°，与发射间隔一起决定螺旋密度）</summary>
        private const float AngleStep = 0.168f;
        private const int EmitInterval = 2;
        private const float BoltSpeed = 6.6f;

        private EmpressStateContext Context;
        private Vector2 weaveAnchor;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
            weaveAnchor = context.Npc.Center;
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            bool weaving = Timer < WeaveTime;

            //织网期：锚点缓移到玩家上方，她本体沿锚点做正弦侧摆，摆动调制织纹
            if (target.Alives()) {
                weaveAnchor = Vector2.Lerp(weaveAnchor, target.Center + new Vector2(0f, -330f), 0.02f);
            }
            float swayAmp = context.IsSecondPhase ? 120f : 70f;
            float sway = (float)Math.Sin(Timer * 0.041f) * swayAmp * (weaving ? 1f : 0.3f);
            GlideTo(npc, weaveAnchor + new Vector2(sway, (float)Math.Sin(Timer * 0.027f) * 26f), 0.03f, 0.12f, 18f);

            if (weaving) {
                context.Pose = EmpressPose.CastBoth;
                context.PoseTimer = 20f;
                //织网通体蓄力感：进度沿时间缓收
                context.SetChargeState(3, 0.35f + 0.3f * (float)Math.Sin(Timer * 0.08f));
                EmpressMotion.HandChargeDust(context.LeftHand, 0.5f, context.DayFormBlend);
                EmpressMotion.HandChargeDust(context.RightHand, 0.5f, context.DayFormBlend);

                if (Timer == 4) {
                    PlayLocal(SoundID.Item164 with { Volume = 0.9f }, npc.Center);
                }
                if (Timer == WeaveTime / 2) {
                    PlayLocal(SoundID.Item165 with { Volume = 0.7f, Pitch = 0.25f }, npc.Center);
                }

                //双臂反向螺旋：k递增角、左右手镜像，速度恒定，纯几何
                if (Timer % EmitInterval == 0 && !VaultUtils.isClient) {
                    int k = Timer / EmitInterval;
                    float thetaL = k * AngleStep;
                    float thetaR = -k * AngleStep + MathHelper.Pi;
                    float hue = (k * 0.029f) % 1f;

                    EmpressCast.Bolt(npc, context.LeftHand, thetaL.ToRotationVector2() * BoltSpeed,
                        context.BoltDamage, 0, hue);
                    EmpressCast.Bolt(npc, context.RightHand, thetaR.ToRotationVector2() * BoltSpeed,
                        context.BoltDamage, 0, hue + 0.5f);

                    //双层珠帘：同角一枚慢速珍珠垫后，双壁螺旋让莫尔纹有厚度
                    EmpressCast.Bolt(npc, context.LeftHand, thetaL.ToRotationVector2() * (BoltSpeed * 0.78f),
                        context.BoltDamage, 0, (hue + 0.04f) % 1f);
                    EmpressCast.Bolt(npc, context.RightHand, thetaR.ToRotationVector2() * (BoltSpeed * 0.78f),
                        context.BoltDamage, 0, (hue + 0.54f) % 1f);

                    //二阶段第二对旋臂：错半步长，织纹加密成四臂莫尔
                    if (context.IsSecondPhase && k % 2 == 0) {
                        float thetaL2 = k * (AngleStep + 0.021f) + MathHelper.PiOver2;
                        float thetaR2 = -k * (AngleStep + 0.021f) - MathHelper.PiOver2;
                        EmpressCast.Bolt(npc, context.LeftHand, thetaL2.ToRotationVector2() * (BoltSpeed * 0.82f),
                            context.BoltDamage, 0, hue + 0.25f);
                        EmpressCast.Bolt(npc, context.RightHand, thetaR2.ToRotationVector2() * (BoltSpeed * 0.82f),
                            context.BoltDamage, 0, hue + 0.75f);
                    }
                }
            }
            else {
                context.Pose = EmpressPose.Idle;
                context.PoseTimer = 0f;
                context.ResetChargeState();
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);

            if (Timer >= TotalTime) {
                return new EmpressConnectorState();
            }
            return null;
        }
    }
}
