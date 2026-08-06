using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>一阶段 dash 中，微弧追踪→末段急停甩头</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.SpazmatismDashing, typeof(TwinsStateContext))]
    internal class SpazmatismDashingState : TwinsStateBase
    {
        public override string StateName => "SpazmatismDashing";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.SpazmatismDashing;

        private const int FullSpeedTime = 18;
        private const int BrakeTime = 12;

        /// <summary>段间复位喘息，无伤，给玩家读招间隔</summary>
        private const int SettleTime = 12;

        private const int DashDuration = FullSpeedTime + BrakeTime + SettleTime;

        private int currentDashCount;
        private int maxDashCount;
        private int comboStep;

        public SpazmatismDashingState() : this(0, 2, 0) {
        }

        public SpazmatismDashingState(int dashCount, int maxCount, int currentComboStep = 0) {
            currentDashCount = dashCount;
            maxDashCount = maxCount;
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            //冲刺状态启用碰撞伤害，低速自动关
            EnableContactDamageIfFast(context.Npc);
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            if (Timer <= FullSpeedTime) {
                //全速微弧
                float speed = npc.velocity.Length();
                TwinsMotion.CurveChase(npc, player.Center, speed, 0.012f);
                EnableContactDamageIfFast(npc);
                FaceVelocity(npc);
                context.PushDashVisuals(1f, 1f);

                //火焰拖尾
                if (!VaultUtils.isServer && Timer % 2 == 0) {
                    PRTLoader.NewParticle<PRT_TwinsSpark>(
                        npc.Center - npc.velocity.SafeNormalize(Vector2.Zero) * 30f + Main.rand.NextVector2Circular(12, 12),
                        -npc.velocity * 0.15f, Color.White, Main.rand.NextFloat(1f, 1.6f))?.Configure(14, 1);
                }
            }
            else if (Timer <= FullSpeedTime + BrakeTime) {
                //急停甩头，关碰撞伤
                DisableContactDamage(npc);
                TwinsMotion.BrakeAndWhip(npc, player.Center, 0.8f, 0.3f);
                context.PushDashVisuals(0.3f, 0.6f);
            }
            else {
                //复位喘息，飘回侧上方再攻位
                DisableContactDamage(npc);
                Vector2 resetPos = player.Center
                    + new Vector2(npc.Center.X < player.Center.X ? -380 : 380, -220);
                TwinsMotion.SpringHover(npc, resetPos, 0.012f, 0.1f, 18f);
                FaceTarget(npc, player.Center);

                //排气余烬
                if (!VaultUtils.isServer && Timer % 4 == 0) {
                    PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center + Main.rand.NextVector2Circular(18, 18),
                        new Vector2(0, -1.8f), Color.White, Main.rand.NextFloat(0.7f, 1.1f))?.Configure(13, 1);
                }
            }

            //冲刺结束
            if (Timer >= DashDuration) {
                currentDashCount++;

                if (currentDashCount >= maxDashCount) {
                    //冲刺次数用完，回到悬停继续套路循环
                    return new SpazmatismHoverShootState(comboStep);
                }
                else {
                    //继续下一次冲刺准备
                    return new SpazmatismDashPrepareState(currentDashCount, comboStep);
                }
            }

            return null;
        }

        public override void OnExit(TwinsStateContext context) {
            base.OnExit(context);
            //离开冲刺状态禁用碰撞伤害
            DisableContactDamage(context.Npc);
        }
    }
}
