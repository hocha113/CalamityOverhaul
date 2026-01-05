using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>
    /// 魔焰眼二阶段冲刺准备状态
    /// </summary>
    internal class SpazmatismPhase2DashPrepareState : TwinsStateBase
    {
        public override string StateName => "SpazmatismPhase2DashPrepare";

        private int ChargeTime => Context.IsMachineRebellion ? 20 : (Context.IsDeathMode ? 25 : 30);
        private int DashCountMax => Context.IsMachineRebellion ? 6 : (Context.IsDeathMode ? 5 : 4);
        private float DashSpeed => Context.IsMachineRebellion ? 40f : (Context.IsDeathMode ? 38f : 35f);

        private TwinsStateContext Context;
        private int dashCount;

        public SpazmatismPhase2DashPrepareState(int currentDashCount = 0) {
            dashCount = currentDashCount;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //减速并面向玩家
            npc.velocity *= 0.92f;
            FaceTarget(npc, player.Center);

            //设置蓄力特效
            context.SetChargeState(1, Timer / (float)ChargeTime);

            //蓄力粒子效果
            if (Timer % 4 == 0 && !VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(40, 40);
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.SolarFlare, 0, 0, 100, default, 1.6f);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                }
            }

            Timer++;

            //蓄力完成
            if (Timer >= ChargeTime) {
                SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                context.ResetChargeState();

                //设置冲刺速度
                npc.velocity = GetDirectionToTarget(context) * DashSpeed;

                return new SpazmatismPhase2DashingState(dashCount, DashCountMax);
            }

            return null;
        }
    }
}
