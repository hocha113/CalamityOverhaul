using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 日舞：她敛势静立，径向光束自身周绽开旋切——
    /// 一阶段同向三阕，二阶段双扇反向，扇间即舞池
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.RadiantDance, typeof(EmpressStateContext))]
    internal class EmpressRadiantDanceState : EmpressStateBase
    {
        public override string StateName => "EmpressRadiantDance";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.RadiantDance;

        private int VolleyCount => Context.IsSecondPhase ? 2 : 3;
        private int VolleyInterval => Context.Scaled(58);
        private int TotalTime => VolleyCount * VolleyInterval + EmpressSunray.TotalLife + Context.Scaled(24);

        private EmpressStateContext Context;

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            //近乎静场：只随呼吸微沉，光束的旋转是唯一的动
            if (target.Alives()) {
                GlideTo(npc, target.Center + new Vector2(0f, -420f) + EmpressMotion.Breathing(1.2f, 18f), 0.006f, 0.1f, 5f);
            }
            else {
                npc.velocity *= 0.94f;
            }

            //长引舞姿（原版日舞臂帧窗口）
            context.Pose = EmpressPose.Dance;
            context.PoseTimer = MathHelper.Clamp(Timer, 10f, 170f);

            int volleyIdx = Timer / VolleyInterval;
            int beat = Timer % VolleyInterval;

            if (volleyIdx < VolleyCount) {
                //起手蓄势→落拍绽束
                if (beat > VolleyInterval - 16) {
                    context.SetChargeState(3, (beat - (VolleyInterval - 16)) / 16f);
                }
                if (beat == VolleyInterval - 1) {
                    CastVolley(context, npc, target, volleyIdx);
                }
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);

            if (Timer >= TotalTime) {
                return new EmpressConnectorState();
            }
            return null;
        }

        /// <summary>绽一阕日舞：P1 同向扇渐进错位；P2 双扇反向交切</summary>
        private void CastVolley(EmpressStateContext context, NPC npc, Player target, int volleyIdx) {
            PlayLocal(SoundID.Item159 with { Volume = 0.95f, Pitch = volleyIdx * 0.1f }, npc.Center);
            EmpressMotion.Shake(npc.Center, 3.4f, 12);

            if (VaultUtils.isClient) {
                return;
            }

            EmpressCast.Radiance(npc, npc.Center, 170f, 20, 0.12f + volleyIdx * 0.3f);

            //基准角向玩家的反侧偏移，第一束永远不压脸（公平阀）
            float baseOffset = target.Alives()
                ? (target.Center - npc.Center).ToRotation() + MathHelper.Pi / 6f
                : 0f;

            if (!context.IsSecondPhase) {
                //一阶段：7束同向，逐阕错半距
                int rays = 7;
                float sweep = (context.IsDeathMode ? 0.0086f : 0.0074f) * (volleyIdx % 2 == 0 ? 1f : -1f);
                for (int i = 0; i < rays; i++) {
                    float angle = baseOffset + MathHelper.TwoPi / rays * (i + volleyIdx * 0.5f);
                    EmpressCast.Sunray(npc, angle, sweep, context.SunrayDamage);
                }
            }
            else {
                //二阶段：6+6双扇反向，扇面交切出开合的菱格；两阕叠加时错开基准角
                int rays = 6;
                float sweep = context.IsDeathMode ? 0.0068f : 0.0058f;
                float volleyShift = volleyIdx * MathHelper.TwoPi / rays * 0.33f;
                for (int i = 0; i < rays; i++) {
                    float angle = baseOffset + volleyShift + MathHelper.TwoPi / rays * i;
                    EmpressCast.Sunray(npc, angle, sweep, context.SunrayDamage);
                    EmpressCast.Sunray(npc, angle + MathHelper.TwoPi / rays * 0.5f, -sweep, context.SunrayDamage);
                }
            }
        }
    }
}
