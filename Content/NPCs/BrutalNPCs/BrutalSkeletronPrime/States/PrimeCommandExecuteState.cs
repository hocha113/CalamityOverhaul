using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>指令执行窗口</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.CommandExecute, typeof(PrimeStateContext))]
    internal class PrimeCommandExecuteState : PrimeStateBase
    {
        public override string StateName => "CommandExecute";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.CommandExecute;

        private readonly PrimeCommandKind command;
        /// <summary>到时锁存指令</summary>
        private PrimeCommandKind resolvedCommand;

        public PrimeCommandExecuteState() : this(PrimeCommandKind.None) { }

        public PrimeCommandExecuteState(PrimeCommandKind command) {
            this.command = command;
        }

        public override void OnEnter(PrimeStateContext context) {
            base.OnEnter(context);
            PrimeCommandKind active = command;
            if (active == PrimeCommandKind.None) {
                active = (PrimeCommandKind)(int)context.Npc.ai[PrimeAiSlots.HeadCommandSlot];
            }
            context.Npc.ai[PrimeAiSlots.HeadCommandSlot] = (float)active;
            if (!VaultUtils.isServer && active != PrimeCommandKind.None) {
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.4f, Volume = 0.7f }, context.Npc.Center);
            }
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 0;
            npc.velocity *= 0.92f;
            LeanByVelocity(npc);

            int duration = PrimeDirector.CommandExecuteFrames;
            context.SetChargeState(1, Timer / (float)duration);

            Timer++;
            if (Timer >= duration && !VaultUtils.isClient) {
                //到时锁存并撤指令
                if (resolvedCommand == PrimeCommandKind.None) {
                    resolvedCommand = (PrimeCommandKind)(int)npc.ai[PrimeAiSlots.HeadCommandSlot];
                    npc.ai[PrimeAiSlots.HeadCommandSlot] = 0f;
                    npc.netUpdate = true;
                }

                //下一手接管编队且臂在收尾蓄力
                //悬停等预警兑现
                bool nextHijacksArms = resolvedCommand is PrimeCommandKind.PhysicalAssault
                    or PrimeCommandKind.FireSuppression;
                if (nextHijacksArms && PrimeFacts.AnyArmCommitted()) {
                    return null;
                }

                return ResolveNext(resolvedCommand);
            }
            return null;
        }

        private static IPrimeState ResolveNext(PrimeCommandKind cmd) => cmd switch {
            PrimeCommandKind.PhysicalAssault => new PrimeSpinDashState(),
            PrimeCommandKind.FireSuppression => new PrimeBarrageCommandState(),
            _ => new PrimeCommandSequenceState(),
        };
    }
}
