using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>
    /// 魔焰眼一阶段冲刺准备状态
    /// </summary>
    internal class SpazmatismDashPrepareState : TwinsStateBase
    {
        public override string StateName => "SpazmatismDashPrepare";

        private const int ChargeTime = 45;
        private const int MaxDashCount = 2;
        private float DashSpeed => Context.IsMachineRebellion ? 30f : 24f;

        private TwinsStateContext Context;
        private int currentDashCount;

        public SpazmatismDashPrepareState(int dashCount = 0) {
            currentDashCount = dashCount;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //减速并面向玩家
            npc.velocity *= 0.9f;
            FaceTarget(npc, player.Center);

            //设置蓄力特效
            context.SetChargeState(1, Timer / (float)ChargeTime);

            //蓄力期间产生火焰粒子
            if (Timer % 5 == 0 && !VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(30, 30);
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.Torch, 0, 0, 100, default, 1.5f);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * 3f;
                }
            }

            Timer++;

            //蓄力完成，开始冲刺
            if (Timer >= ChargeTime) {
                SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                context.ResetChargeState();

                //设置冲刺速度
                npc.velocity = GetDirectionToTarget(context) * DashSpeed;

                return new SpazmatismDashingState(currentDashCount, MaxDashCount);
            }

            return null;
        }
    }
}
