using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>
    /// 冲刺蓄力状态：减速制动→对准目标→粒子聚集→震动→释放
    /// </summary>
    internal class DestroyerDashPrepareState : DestroyerStateBase
    {
        public override string StateName => "DashPrepare";

        private int ChargeTime(DestroyerStateContext ctx) => ctx.IsEnraged ? 35 : 50;
        private float DashSpeed(DestroyerStateContext ctx) => ctx.IsEnraged ? 55f : 42f;
        private int MaxDashCount(DestroyerStateContext ctx) => ctx.IsEnraged ? 5 : 3;

        private int currentDashCount;
        private Vector2 dashDirection;

        public DestroyerDashPrepareState(int dashCount = 0) {
            currentDashCount = dashCount;
        }

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //减速制动
            npc.velocity *= 0.92f;

            //对准
            Vector2 toPlayer = player.Center - npc.Center;
            dashDirection = toPlayer.SafeNormalize(Vector2.UnitY);
            FaceTarget(npc, player.Center, 0.15f);

            //蓄力进度
            int chargeTime = ChargeTime(context);
            float progress = Math.Min(Timer / (float)chargeTime, 1f);
            context.SetChargeState(1, progress);
            context.DashDirection = dashDirection;

            //蓄力粒子
            if (!VaultUtils.isServer && Timer % 3 == 0) {
                for (int i = 0; i < (int)(progress * 5) + 1; i++) {
                    Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(40, 40);
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1,
                        DustID.FireworkFountain_Red, 0, 0, 100, default, 1.5f + progress);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * (2f + progress * 4f);
                }
            }

            //蓄力后期震动
            if (progress > 0.6f && !VaultUtils.isServer) {
                float shakeMagnitude = (progress - 0.6f) * 5f;
                npc.Center += Main.rand.NextVector2Circular(shakeMagnitude, shakeMagnitude);
            }

            Timer++;

            if (Timer >= chargeTime) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f }, npc.Center);
                context.ResetChargeState();

                npc.velocity = dashDirection * DashSpeed(context);
                return new DestroyerDashingState(currentDashCount, MaxDashCount(context));
            }

            return null;
        }

        public override void OnExit(DestroyerStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
        }
    }
}
