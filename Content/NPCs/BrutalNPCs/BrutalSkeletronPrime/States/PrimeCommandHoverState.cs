using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 指挥悬停：武装阶段常态。头部压阵在玩家上方，输出交给四条机械臂；
    /// 悬停尾声进入蓄力预警，随后按固定序列切换到冲撞或机械风暴。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.CommandHover, typeof(PrimeStateContext))]
    internal class PrimeCommandHoverState : PrimeStateBase
    {
        public override string StateName => "CommandHover";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.CommandHover;

        private const int TelegraphTime = 60;

        private int HoverDuration(PrimeStateContext ctx) {
            int duration = ctx.MasterMode ? 540 : 660;
            if (ctx.DeathMode) {
                duration -= 120;
            }
            if (ctx.BossRush) {
                duration -= 120;
            }
            return System.Math.Max(duration, 300);
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 0;

            //风暴领域意外残留（中途加入/状态回退）：直接交给风暴状态接管
            if (context.StormCount > 0 && !VaultUtils.isClient) {
                return new PrimeMechStormState();
            }

            Movement(context);
            LeanByVelocity(npc);

            int duration = HoverDuration(context);

            //尾声蓄力预警——机体泛红、热能脉冲，向玩家宣告冲撞将至
            int remaining = duration - Timer;
            if (remaining <= TelegraphTime) {
                context.SetChargeState(1, 1f - remaining / (float)TelegraphTime);
            }

            Timer++;
            if (Timer >= duration && !VaultUtils.isClient) {
                npc.TargetClosest();
                npc.netUpdate = true;
                return ChooseNextAttack(context);
            }
            return null;
        }

        /// <summary>
        /// 固定出招序列：冲撞 → 机械风暴 → 冲撞 → ……
        /// 风暴需要双子在场且场上没有毁灭者（本体或领域），否则顺延为冲撞
        /// </summary>
        private IPrimeState ChooseNextAttack(PrimeStateContext context) {
            bool wantStorm = context.AttackPhaseIndex % 2 == 1;
            context.AttackPhaseIndex++;

            if (wantStorm && !context.NoEye && context.StormCount == 0
                && CWRUtils.FindNPCFromeType(NPCID.TheDestroyer) == null) {
                return new PrimeMechStormState();
            }
            return new PrimeSpinDashState();
        }

        private void Movement(PrimeStateContext context) {
            float vAccel = 0.1f;
            float vMax = 2f;
            float hAccel = 0.1f;
            float hMax = 8f;
            float decel = Main.masterMode ? 0.94f : Main.expertMode ? 0.96f : 0.98f;

            if (Main.expertMode) {
                vAccel = Main.masterMode ? 0.04f : 0.03f;
                vMax = Main.masterMode ? 5f : 4f;
                hAccel = Main.masterMode ? 0.1f : 0.08f;
                hMax = Main.masterMode ? 10f : 9.5f;
                if (context.DeathMode) {
                    vAccel += 0.01f;
                    vMax += 0.3f;
                    hAccel += 0.1f;
                    hMax += 1f;
                }
                if (context.BossRush) {
                    vAccel += 0.01f;
                    vMax += 0.5f;
                    hAccel += 0.1f;
                    hMax += 1f;
                }
            }

            HoverMovement(context, vAccel, vMax, hAccel, hMax, decel, 200, 500);
        }
    }
}
