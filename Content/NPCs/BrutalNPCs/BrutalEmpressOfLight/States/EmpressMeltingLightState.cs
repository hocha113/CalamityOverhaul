using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 熔光扇：30f 起手后每 45f 放一组七射线扇，每组 120f 充能，同屏两三组交错；
    /// 扇从"瞄准角+扫角"转回瞄准角，方向每三组反一次；她在昼形态慢慢挪向玩家前方 400px，安全楔随之在动
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.MeltingLight, typeof(EmpressStateContext))]
    internal class EmpressMeltingLightState : EmpressStateBase
    {
        public override string StateName => "EmpressMeltingLight";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.MeltingLight;

        private const int StartDelay = 30;
        private const int Interval = 45;
        private int FanCount => Context.DayEmpowered ? (Context.IsSecondPhase ? 8 : 6) : 4;
        private int TotalTime => StartDelay + Interval * (FanCount - 1) + EmpressMeltingFan.TotalTime + 20;

        private EmpressStateContext Context;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
            PlayLocal(SoundID.Item165 with { Volume = 0.8f, Pitch = -0.35f }, context.Npc.Center);
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            int castTick = Timer - StartDelay;
            int lastCast = Interval * (FanCount - 1);
            bool casting = castTick >= 0 && castTick <= lastCast + EmpressMeltingFan.ChargeTime;

            //姿势：充能期双手高举，最后一组发射后收手
            context.Pose = casting ? EmpressPose.CastBoth : EmpressPose.Idle;
            context.PoseTimer = casting ? 40f : 0f;
            if (casting) {
                float charge = ((castTick % Interval) / (float)Interval);
                context.SetChargeState(3, charge);
                if (!VaultUtils.isServer) {
                    EmpressMotion.HandChargeDust(context.LeftHand, charge, context.DayFormBlend);
                    EmpressMotion.HandChargeDust(context.RightHand, charge, context.DayFormBlend);
                    EmpressScreenFX.DeclareAmbient(0.35f);
                }
            }

            //移动：昼慢慢挪向玩家前方 400px（原点在动，安全楔在动）；夜原地
            if (target.Alives() && castTick >= 0) {
                Vector2 toT = npc.DirectionTo(target.Center);
                Vector2 anchor = target.Center - toT * 400f;
                float dist = npc.Distance(anchor);
                Vector2 desired = context.DayEmpowered ? toT * MathF.Min(dist * 0.06f, 6f) : Vector2.Zero;
                float ramp = MathHelper.Clamp(castTick / 80f, 0f, 1f);
                npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.125f * ramp * ramp);
            }
            else {
                npc.velocity *= 0.9f;
            }

            if (castTick >= 0 && castTick <= lastCast && castTick % Interval == 0 && target.Alives() && !VaultUtils.isClient) {
                CastFan(context, npc, target, castTick / Interval);
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);
            if (Timer >= TotalTime) {
                return new EmpressConnectorState();
            }
            return null;
        }

        private void CastFan(EmpressStateContext context, NPC npc, Player target, int idx) {
            //瞄预测点：相对速度 × 剩余充能帧，最远不超过当前距离（且不小于 375）
            Vector2 lead = (target.velocity - npc.velocity) * EmpressMeltingFan.ChargeTime;
            float cap = MathF.Max(npc.Distance(target.Center), 375f);
            if (lead.Length() > cap) {
                lead = lead.SafeNormalize(Vector2.Zero) * cap;
            }
            float aim = npc.AngleTo(target.Center + lead);
            float sweep = (idx & 1) == 1 ? 1.68f : 1.32f;
            sweep *= context.DayEmpowered ? 1.25f : 1f;
            if (idx % 3 == 2) {
                sweep = -sweep;
            }
            //从 aim+sweep 转回 aim
            EmpressCast.Fan(npc, aim + sweep, -sweep, context.FanDamage);
        }
    }
}
