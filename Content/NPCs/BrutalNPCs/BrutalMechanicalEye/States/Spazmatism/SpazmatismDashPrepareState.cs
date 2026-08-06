using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>一阶段 dash 准备，后撤蓄力→锁向→爆发起步</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.SpazmatismDashPrepare, typeof(TwinsStateContext))]
    internal class SpazmatismDashPrepareState : TwinsStateBase
    {
        public override string StateName => "SpazmatismDashPrepare";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.SpazmatismDashPrepare;

        private int ChargeTime => Context.IsDeathMode ? 40 : 48;
        private int MaxDashCount => Context.IsDeathMode ? 3 : 2;
        private float DashSpeed => Context.IsDeathMode ? 36f : 32f;

        private TwinsStateContext Context;
        private int currentDashCount;
        private int comboStep;
        private Vector2 lockedDirection;

        public SpazmatismDashPrepareState() : this(0, 0) {
        }

        public SpazmatismDashPrepareState(int dashCount, int currentComboStep = 0) {
            currentDashCount = dashCount;
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            float progress = Timer / (float)ChargeTime;

            //后撤蓄力前70%
            Vector2 awayDir = (npc.Center - player.Center).SafeNormalize(Vector2.UnitY);
            if (progress < 0.7f) {
                float pull = VaultUtils.EaseInQuad(progress / 0.7f);
                npc.velocity = npc.velocity * 0.86f + awayDir * pull * 4.2f;
            }
            else {
                //绷紧颤抖
                npc.velocity *= 0.72f;
                if (!VaultUtils.isServer) {
                    npc.position += Main.rand.NextVector2Circular(1.6f, 1.6f);
                }
            }
            FaceTarget(npc, player.Center);

            //设置蓄力特效
            context.SetChargeState(1, progress);

            //能量内聚粒子
            if (Timer % 3 == 0) {
                TwinsMotion.ChargeGatherFX(npc.Center, true, progress, 80f);
            }
            if (Timer % 5 == 0 && !VaultUtils.isServer) {
                Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(30, 30);
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.Torch, 0, 0, 100, default, 1.5f);
                dust.noGravity = true;
                dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * 3f;
            }

            Timer++;

            //蓄力完成，瞬时爆发起步
            if (Timer >= ChargeTime) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                }
                context.ResetChargeState();

                //轻微预判玩家走位
                Vector2 predicted = TwinsMotion.PredictTarget(player, npc.Center, DashSpeed, 0.5f);
                lockedDirection = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);

                //瞬时加速到峰值+音爆演出
                TwinsMotion.DashLaunch(npc, lockedDirection, DashSpeed, spazTheme: true);
                context.PushDashVisuals(1f, 1f);
                npc.netUpdate = true;
                return new SpazmatismDashingState(currentDashCount, MaxDashCount, comboStep);
            }

            return null;
        }
    }
}
