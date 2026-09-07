using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 瞬现枪：读玩家速度，把线放在他身后并顺着飞行方向打。直线飞是最危险的选择。
    /// 一阶段散射（横铺 ±2750 的一大片，晚生的先到，145~160f 成波打出）；二阶段先五枪一列横扫三组再补一片散射
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.HitscanVolley, typeof(EmpressStateContext))]
    internal class EmpressHitscanVolleyState : EmpressStateBase
    {
        public override string StateName => "EmpressHitscanVolley";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.HitscanVolley;

        private const int SpreadStart = 60;
        private const int SpreadEnd = 160;
        private const int LinesEnd = 120;

        private bool Lines => Context.IsSecondPhase;
        private int TotalTime => (Lines ? LinesEnd + 60 : 0) + SpreadEnd + 60;

        private EmpressStateContext Context;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
            PlayLocal(SoundID.Item164 with { Volume = 0.7f, Pitch = 0.1f }, context.Npc.Center);
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            context.Pose = EmpressPose.CastLeft;
            context.PoseTimer = MathHelper.Clamp(Timer % 60, 0f, 60f);

            if (target.Alives()) {
                Vector2 dest = target.Center + new Vector2(target.Center.X > npc.Center.X ? -500f : 500f, -250f);
                npc.velocity = npc.velocity * 0.7f + (dest - npc.Center) * 0.042f;
            }
            else {
                npc.velocity *= 0.9f;
            }

            if (target.Alives() && !VaultUtils.isClient) {
                int t = Timer;
                if (Lines) {
                    if (t < LinesEnd) {
                        CastLines(context, npc, target, t);
                    }
                    t -= LinesEnd + 60;
                }
                if (t >= SpreadStart && t < SpreadEnd) {
                    CastSpread(context, npc, target, t);
                }
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);
            if (Timer >= TotalTime) {
                return new EmpressConnectorState();
            }
            return null;
        }

        /// <summary>玩家速度方向；静止时用计时器角兜底，别让它无解地对准脸</summary>
        private static Vector2 MoveDir(Player target, int t) {
            Vector2 v = target.velocity.SafeNormalize(Vector2.Zero);
            return v == Vector2.Zero ? (t * 0.37f).ToRotationVector2() : v;
        }

        private void CastSpread(EmpressStateContext context, NPC npc, Player target, int t) {
            int every = context.DayEmpowered ? 2 : 4;
            if (t % every != 0) {
                return;
            }
            Vector2 dir = MoveDir(target, t);
            float back = MathHelper.Clamp(target.velocity.Length() * 20f, 800f, 1200f);
            //横铺：t%17 决定横向格位，覆盖 ±2750
            Vector2 lateral = dir.RotatedBy(-MathHelper.PiOver2) * ((t % 17 - 8.5f) / 8.5f * 2750f);
            Vector2 pos = target.Center - dir * back + lateral;
            int fireDelay = (int)((SpreadEnd - t) * 0.85f) + 12;
            EmpressCast.Hitscan(npc, pos, dir.ToRotation(), context.HitscanDamage, fireDelay);
        }

        private void CastLines(EmpressStateContext context, NPC npc, Player target, int t) {
            int k = t % 40;
            if (k > 16 || t % 4 != 0) {
                return;
            }
            Vector2 dir = target.velocity.SafeNormalize(Vector2.Zero);
            if (dir == Vector2.Zero) {
                dir = new Vector2(target.direction, 0f);
            }
            float back = MathHelper.Clamp(target.velocity.Length() * 30f, 600f, 1200f);
            Vector2 lateral = dir.RotatedBy(-MathHelper.PiOver2) * (-650f + 1300f * (k / 16f));
            Vector2 pos = target.Center - dir * back + lateral;
            int fireDelay = (int)(40f - k * 0.33f) + 8;
            EmpressCast.Hitscan(npc, pos, dir.ToRotation(), context.HitscanDamage, fireDelay);
        }
    }
}
