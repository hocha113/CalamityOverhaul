using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.States
{
    /// <summary>连接态：∞字蛇行绕体，就位即出招；出招表在此裁定</summary>
    [InnoVault.StateMachines.VaultState((int)EowStateIndex.Weave, typeof(EowStateContext))]
    internal class EowWeaveState : EowStateBase
    {
        public override string StateName => "Weave";
        public override EowStateIndex StateIndex => EowStateIndex.Weave;

        private int WeaveDuration(EowStateContext ctx) => ctx.IsPhase2 ? 78 : 104;

        public EowWeaveState() {
        }

        public override IEowState OnUpdate(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Tick();

            //∞字蛇行(利萨如轨迹)，绕玩家上半环游走
            float t = Timer * 0.02f;
            float offsetX = (float)Math.Cos(t) * 760f;
            float offsetY = (float)Math.Sin(t * 2f) * 250f - 260f;
            Vector2 weaveTarget = player.Center + new Vector2(offsetX, offsetY);

            float accelT = Math.Min(Timer / 50f, 1f);
            float speed = MathHelper.Lerp(11f, context.IsPhase2 ? 21f : 17f, accelT);
            SetMovement(context, weaveTarget, speed, 1.25f);
            context.SlitherStrength = 1f;
            context.AccelRate = 0.08f;

            int duration = WeaveDuration(context);
            //就位提前出招
            bool positioned = Timer > duration * 0.5f
                && npc.WithinRange(weaveTarget, 260f)
                && npc.Distance(player.Center) < 1300f;

            if (Timer > duration || positioned) {
                if (!VaultUtils.isClient) {
                    return ChooseNextAttack(context);
                }
            }

            return null;
        }

        /// <summary>出招裁定：蜕皮/大招节点优先，其余走手排出招环</summary>
        private IEowState ChooseNextAttack(EowStateContext context) {
            NPC npc = context.Npc;
            float lifeRatio = npc.lifeMax > 0 ? npc.life / (float)npc.lifeMax : 1f;

            //蜕皮节点
            if (!context.MoltDone && lifeRatio <= EowHeadAI.MoltThreshold) {
                return new EowMoltTransitionState();
            }

            //大招节点：首破阈值立即释放，之后并入循环
            if (context.MoltDone && lifeRatio <= EowHeadAI.ApexThreshold && !context.ApexCycleStarted) {
                context.ApexCycleStarted = true;
                context.AttackPhaseIndex = 0;
                return new EowApexFrenzyState();
            }

            //手排出招环：压制↔机动↔伏击交替
            IEowState[] sequence;
            if (context.ApexCycleStarted) {
                sequence = [
                    new EowSplitPincerState(),
                    new EowAcidRainState(),
                    new EowLungeFlurryState(),
                    new EowGeyserRakeState(),
                    new EowHuskMinesState(),
                    new EowBurrowAmbushState(),
                    new EowSpitBarrageState(),
                    new EowApexFrenzyState(),
                ];
            }
            else if (context.IsPhase2) {
                sequence = [
                    new EowAcidRainState(),
                    new EowLungeFlurryState(),
                    new EowSplitPincerState(),
                    new EowSpitBarrageState(),
                    new EowGeyserRakeState(),
                    new EowBurrowAmbushState(),
                    new EowHuskMinesState(),
                    new EowLungeFlurryState(),
                ];
            }
            else {
                sequence = [
                    new EowSpitBarrageState(),
                    new EowLungeFlurryState(),
                    new EowBurrowAmbushState(),
                    new EowHuskMinesState(),
                    new EowGeyserRakeState(),
                    new EowSplitPincerState(),
                ];
            }

            IEowState next = sequence[context.AttackPhaseIndex % sequence.Length];
            context.AttackPhaseIndex++;
            return next;
        }
    }
}
