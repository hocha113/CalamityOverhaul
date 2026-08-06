using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>二阶段 dash 准备，更短蓄力+更猛爆发</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.SpazmatismPhase2DashPrepare, typeof(TwinsStateContext))]
    internal class SpazmatismPhase2DashPrepareState : TwinsStateBase
    {
        public override string StateName => "SpazmatismPhase2DashPrepare";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.SpazmatismPhase2DashPrepare;

        private int ChargeTime => Context.IsDeathMode ? 36 : 42;
        private int DashCountMax => Context.IsDeathMode ? 4 : 3;
        private float DashSpeed => Context.IsDeathMode ? 44f : 40f;

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
            float progress = Timer / (float)ChargeTime;

            //短促后撤蓄力，末段绷紧颤抖
            Vector2 awayDir = (npc.Center - player.Center).SafeNormalize(Vector2.UnitY);
            if (progress < 0.6f) {
                float pull = VaultUtils.EaseInQuad(progress / 0.6f);
                npc.velocity = npc.velocity * 0.84f + awayDir * pull * 5f;
            }
            else {
                npc.velocity *= 0.68f;
                if (!VaultUtils.isServer) {
                    npc.position += Main.rand.NextVector2Circular(2.2f, 2.2f);
                }
            }
            FaceTarget(npc, player.Center);

            //设置蓄力特效
            context.SetChargeState(1, progress);

            //能量内聚粒子(二阶段更密)
            if (Timer % 2 == 0) {
                TwinsMotion.ChargeGatherFX(npc.Center, true, progress, 95f);
            }
            if (Timer % 4 == 0 && !VaultUtils.isServer) {
                Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(40, 40);
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.SolarFlare, 0, 0, 100, default, 1.6f);
                dust.noGravity = true;
                dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * 4f;
            }

            Timer++;

            //蓄力完成，猛烈爆发
            if (Timer >= ChargeTime) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                }
                context.ResetChargeState();

                Vector2 predicted = TwinsMotion.PredictTarget(player, npc.Center, DashSpeed, 0.6f);
                Vector2 dir = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);

                TwinsMotion.DashLaunch(npc, dir, DashSpeed, spazTheme: true, boomStrength: 1.25f);
                context.PushDashVisuals(1f, 1f);
                npc.netUpdate = true;
                return new SpazmatismPhase2DashingState(dashCount, DashCountMax, comboStep);
            }

            return null;
        }
    }
}
