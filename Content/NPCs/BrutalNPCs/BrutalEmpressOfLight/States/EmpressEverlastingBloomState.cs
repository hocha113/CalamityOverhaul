using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 永恒绽放（二阶段专属）：虹瓣双层反旋螺旋铺满战场，
    /// 久驻的虹彩缎带把空间雕成花园，终拍一束追光弹收网
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.EverlastingBloom, typeof(EmpressStateContext))]
    internal class EmpressEverlastingBloomState : EmpressStateBase
    {
        public override string StateName => "EmpressEverlastingBloom";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.EverlastingBloom;

        private int TotalTime => Context.Scaled(300);

        private EmpressStateContext Context;

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            //高位静场：绽放要有舞台
            if (target.Alives()) {
                GlideTo(npc, target.Center + new Vector2(0f, -430f) + EmpressMotion.Breathing(2.1f, 16f), 0.008f, 0.1f, 7f);
            }
            else {
                npc.velocity *= 0.94f;
            }

            int castA = Context.Scaled(40);
            int castB = Context.Scaled(102);
            int castC = Context.Scaled(172);
            int castD = Context.Scaled(212);

            //姿态编排：右手起势→长引→双手收网
            if (Timer < castB + 20) {
                context.Pose = EmpressPose.CastRight;
                context.PoseTimer = 20f;
            }
            else if (Timer < castC) {
                context.Pose = EmpressPose.Dance;
                context.PoseTimer = MathHelper.Clamp(Timer - castB, 10f, 170f);
            }
            else {
                context.Pose = EmpressPose.CastBoth;
                context.PoseTimer = 20f;
            }

            //起势蓄力提示
            if (Timer > castA - 16 && Timer < castA) {
                context.SetChargeState(2, (Timer - (castA - 16)) / 16f);
                EmpressMotion.HandChargeDust(context.RightHand, (Timer - (castA - 16)) / 16f, context.DayFormBlend);
            }

            if (Timer == castA) {
                CastPetalRing(context, npc, 14, 0.0105f, 3.3f, 0f);
                PlayLocal(SoundID.Item163 with { Volume = 1f }, npc.Center);
                EmpressMotion.Shake(npc.Center, 3f, 10);
            }
            if (Timer == castB) {
                //反旋second层，错半距——双层螺旋园
                CastPetalRing(context, npc, 14, -0.0105f, 3.7f, MathHelper.TwoPi / 28f);
                PlayLocal(SoundID.Item163 with { Volume = 0.9f, Pitch = 0.18f }, npc.Center);
            }
            if (Timer == castC) {
                //追光收网：上扬扇形缓追踪弹
                CastHomingFan(context, npc);
                PlayLocal(SoundID.Item164 with { Volume = 0.9f }, npc.Center);
            }
            if (Timer == castD) {
                //终拍紧凑快旋小环
                CastPetalRing(context, npc, 10, 0.017f, 4.4f, 0f);
                PlayLocal(SoundID.Item163 with { Volume = 0.8f, Pitch = 0.35f }, npc.Center);
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);
            //绽放期光雨加密
            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);

            if (Timer >= TotalTime) {
                return new EmpressConnectorState();
            }
            return null;
        }

        /// <summary>绽一层虹瓣环</summary>
        private void CastPetalRing(EmpressStateContext context, NPC npc, int count, float curve, float speed, float angleOffset) {
            if (VaultUtils.isClient) {
                return;
            }
            EmpressCast.Radiance(npc, npc.Center, 150f, 18, curve > 0 ? 0.85f : 0.3f);
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi / count * i + angleOffset;
                Vector2 dir = angle.ToRotationVector2();
                float hue = angle / MathHelper.TwoPi;
                EmpressCast.Petal(npc, npc.Center + dir * 40f, dir * speed, context.PetalDamage, curve, hue,
                    context.IsDeathMode ? 2f : 1f);
            }
        }

        /// <summary>上扬扇形追光弹：短暂缓追踪后放直</summary>
        private void CastHomingFan(EmpressStateContext context, NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            int count = context.IsDeathMode ? 12 : 10;
            for (int i = 0; i < count; i++) {
                //上半扇 -160°..-20°
                float angle = MathHelper.ToRadians(-160f + 140f * (i / (float)(count - 1)));
                Vector2 vel = angle.ToRotationVector2() * 8.6f;
                float hue = i / (float)count;
                EmpressCast.Bolt(npc, npc.Center + new Vector2(0f, -30f), vel, context.BoltDamage, 3, hue, npc.target);
            }
        }
    }
}
