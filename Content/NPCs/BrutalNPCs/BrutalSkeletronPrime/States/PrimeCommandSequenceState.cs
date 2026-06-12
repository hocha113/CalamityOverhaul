using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 武装阶段指挥 hub：~120 帧短悬停 + 战术指令广播，按固定序列分发下一招式。
    /// 序列：物理突击 → SpinDash → 火力压制 → Barrage → 十字绞杀 → SpinDash → TetherSpin
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.CommandSequence, typeof(PrimeStateContext))]
    internal class PrimeCommandSequenceState : PrimeStateBase
    {
        public override string StateName => "CommandSequence";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.CommandSequence;

        internal static int TelegraphLead => 30;

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 0;

            Movement(context);
            LeanByVelocity(npc);

            int hubDuration = PrimeDirector.CommandHubFrames;
            if (context.MasterMode) {
                hubDuration -= 15;
            }
            if (context.BossRush) {
                hubDuration -= 20;
            }

            int remaining = hubDuration - Timer;
            if (remaining <= TelegraphLead) {
                context.SetChargeState(1, 1f - remaining / (float)TelegraphLead);
                if (!VaultUtils.isClient && remaining == TelegraphLead) {
                    BroadcastTelegraph(context);
                }
            }

            Timer++;
            if (Timer >= hubDuration && !VaultUtils.isClient) {
                npc.TargetClosest();
                npc.netUpdate = true;
                return DispatchNext(context);
            }
            return null;
        }

        private static void BroadcastTelegraph(PrimeStateContext context) {
            int step = context.AttackPhaseIndex % 7;

            //只有冲撞步需要空间预警（方向线）；
            //指令步的预告由机体充能热感表达，不放置无躲避语义的视觉
            if (step is 1 or 5) {
                PrimeTelegraphLine.SpawnLine(context.Npc, context.Npc.Center,
                    DirectionToTarget(context).ToRotation(), TelegraphLead);
            }

            PrimeCommandKind cmd = step switch {
                0 => PrimeCommandKind.PhysicalAssault,
                2 => PrimeCommandKind.FireSuppression,
                4 => PrimeCommandKind.CrossExecute,
                _ => PrimeCommandKind.None,
            };
            context.Npc.ai[PrimeAiSlots.HeadCommandSlot] = (float)cmd;
        }

        private static IPrimeState DispatchNext(PrimeStateContext context) {
            int step = context.AttackPhaseIndex % 7;
            context.AttackPhaseIndex++;

            return step switch {
                0 => BeginCommand(context, PrimeCommandKind.PhysicalAssault),
                1 => new PrimeSpinDashState(),
                2 => BeginCommand(context, PrimeCommandKind.FireSuppression),
                3 => new PrimeBarrageCommandState(),
                4 => new PrimeCrossExecuteState(),
                5 => new PrimeSpinDashState(),
                _ => new PrimeTetherSpinState(),
            };
        }

        private static IPrimeState BeginCommand(PrimeStateContext context, PrimeCommandKind kind) {
            context.Npc.ai[PrimeAiSlots.HeadCommandSlot] = (float)kind;
            return new PrimeCommandExecuteState(kind);
        }

        private void Movement(PrimeStateContext context) {
            float vAccel = Main.masterMode ? 0.04f : 0.03f;
            float vMax = Main.masterMode ? 5f : 4f;
            float hAccel = Main.masterMode ? 0.1f : 0.08f;
            float hMax = Main.masterMode ? 10f : 9f;
            float decel = Main.masterMode ? 0.94f : 0.96f;
            HoverMovement(context, vAccel, vMax, hAccel, hMax, decel, 200, 480);
        }
    }

}

