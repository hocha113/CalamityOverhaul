using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>
    /// 魔焰眼二阶段冲刺准备状态
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.SpazmatismPhase2DashPrepare, typeof(TwinsStateContext))]
    internal class SpazmatismPhase2DashPrepareState : TwinsStateBase
    {
        public override string StateName => "SpazmatismPhase2DashPrepare";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.SpazmatismPhase2DashPrepare;

        private int ChargeTime => Context.IsDeathMode ? 30 : 35;
        private int DashCountMax => Context.IsDeathMode ? 4 : 3;
        private float DashSpeed => Context.IsDeathMode ? 35f : 32f;

        private TwinsStateContext Context;
        private int dashCount;
        private int comboStep;

        public SpazmatismPhase2DashPrepareState() : this(0, 0) {
        }

        public SpazmatismPhase2DashPrepareState(int currentDashCount, int currentComboStep = 0) {
            dashCount = currentDashCount;
            comboStep = currentComboStep;
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
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                }
                context.ResetChargeState();

                //设置冲刺速度
                npc.velocity = GetDirectionToTarget(context) * DashSpeed;
                npc.netUpdate = true;
                return new SpazmatismPhase2DashingState(dashCount, DashCountMax, comboStep);
            }

            return null;
        }
    }
}
