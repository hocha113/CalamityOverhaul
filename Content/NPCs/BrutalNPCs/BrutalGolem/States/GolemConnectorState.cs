using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States
{
    /// <summary>出招衔接 hub：手作序列表出招，追近阀防拉扯</summary>
    [InnoVault.StateMachines.VaultState((int)GolemStateIndex.Connector, typeof(GolemStateContext))]
    internal class GolemConnectorState : GolemStateBase
    {
        public override string StateName => "Connector";
        public override GolemStateIndex StateIndex => GolemStateIndex.Connector;

        /// <summary>一阶段手作序列：压迫→区域→机动→远程→大开大合</summary>
        private static readonly Func<IGolemState>[] SequenceP1 = [
            () => new GolemPunchComboState(),
            () => new GolemTrapScoreState(),
            () => new GolemStompComboState(),
            () => new GolemSunBarrageState(),
            () => new GolemHookSwingState(),
        ];

        /// <summary>二阶段手作序列：交叉火力开场，机动与机关穿插</summary>
        private static readonly Func<IGolemState>[] SequenceP2 = [
            () => new GolemCrossfireState(),
            () => new GolemPunchComboState(),
            () => new GolemMeteorLeapState(),
            () => new GolemTrapScoreState(),
            () => new GolemHookSwingState(),
            () => new GolemSunBarrageState(),
        ];

        private bool hopDone;

        public override void OnEnter(GolemStateContext context) {
            base.OnEnter(context);
            hopDone = false;
        }

        public override IGolemState OnUpdate(GolemStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 0;
            GroundBrake(npc);
            RestoreTileCollide(context);

            //追近阀：距离过远小跳追击，不干等时钟
            float dx = context.Target.Center.X - npc.Center.X;
            if (!hopDone && OnGround(npc) && Math.Abs(dx) > 620f && Timer > 8) {
                hopDone = true;
                float vx = MathHelper.Clamp(dx / 60f, -13f, 13f);
                LaunchJump(npc, vx, -9.5f);
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
            }
            if (!OnGround(npc)) {
                AirSteer(context, 0.12f, 14f);
                context.FrameMode = 2;
            }

            Timer++;
            int duration = Tempo(context, context.PostUltRage ? 22 : 38);
            if (Timer >= duration && OnGround(npc) && !VaultUtils.isClient) {
                return PickNext(context);
            }
            return null;
        }

        /// <summary>按阶段序列表出招（服务端）；双拳全灭时循环跳过纯拳招式</summary>
        private static IGolemState PickNext(GolemStateContext context) {
            Func<IGolemState>[] sequence = context.Sundered ? SequenceP2 : SequenceP1;

            for (int attempt = 0; attempt < sequence.Length; attempt++) {
                int index;
                if (context.Sundered) {
                    index = context.AttackIndexP2 % sequence.Length;
                    context.AttackIndexP2++;
                }
                else {
                    index = context.AttackIndexP1 % sequence.Length;
                    context.AttackIndexP1++;
                }

                IGolemState next = sequence[index]();
                if (next is GolemPunchComboState or GolemHookSwingState && context.Limbs.FistCount == 0) {
                    continue;
                }
                return next;
            }
            //兜底：全部被跳过时回落重踏
            return new GolemStompComboState();
        }
    }
}
