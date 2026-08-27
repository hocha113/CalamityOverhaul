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

        /// <summary>一阶段手作序列：压迫→区域→机动→远程→大开大合起头，
        /// 后续小节拳位加密、弹幕位稀释——飞拳 4/15（较均分 +33%），太阳弹幕 2/15（-33%），其余各 3/15</summary>
        private static readonly Func<IGolemState>[] SequenceP1 = [
            () => new GolemPunchComboState(),
            () => new GolemTrapScoreState(),
            () => new GolemStompComboState(),
            () => new GolemSunBarrageState(),
            () => new GolemHookSwingState(),
            () => new GolemPunchComboState(),
            () => new GolemStompComboState(),
            () => new GolemTrapScoreState(),
            () => new GolemPunchComboState(),
            () => new GolemHookSwingState(),
            () => new GolemStompComboState(),
            () => new GolemSunBarrageState(),
            () => new GolemPunchComboState(),
            () => new GolemTrapScoreState(),
            () => new GolemHookSwingState(),
        ];

        /// <summary>二阶段手作序列：交叉火力开场，机动与机关穿插，投技压在中段高潮位</summary>
        private static readonly Func<IGolemState>[] SequenceP2 = [
            () => new GolemCrossfireState(),
            () => new GolemPunchComboState(),
            () => new GolemMeteorLeapState(),
            () => new GolemTrapScoreState(),
            () => new GolemWallSlamState(),
            () => new GolemHookSwingState(),
            () => new GolemSunBarrageState(),
        ];

        private bool hopDone;
        private bool airborne;

        public override void OnEnter(GolemStateContext context) {
            base.OnEnter(context);
            hopDone = false;
            airborne = false;
        }

        public override IGolemState OnUpdate(GolemStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 0;
            GroundBrake(npc);
            RestoreTileCollide(context);

            //追近阀：不再干等时钟，稍有距离就跃向目标（跳跃本身即威胁）
            float dx = context.Target.Center.X - npc.Center.X;
            if (!hopDone && OnGround(npc) && Math.Abs(dx) > 240f && Timer > 8) {
                hopDone = true;
                float vx = MathHelper.Clamp(dx / 60f, -13f, 13f);
                LaunchJump(context, vx, -9.5f);
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
            }
            if (!OnGround(npc)) {
                AirSteer(context, 0.12f, 14f);
                context.FrameMode = 2;
                npc.damage = npc.defDamage;
            }
            else {
                npc.damage = 0;
            }
            if (LandedThisFrame(npc, ref airborne)) {
                LandingImpact(context, context.Sundered ? 3 : 2);
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
                //投技触发阀不满足（冷却/距离/时停等）则跳过本轮
                if (next is GolemWallSlamState && !GolemWallSlamState.GrabReady(context)) {
                    continue;
                }
                return next;
            }
            //兜底：全部被跳过时回落重踏
            return new GolemStompComboState();
        }
    }
}
