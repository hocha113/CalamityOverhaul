using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 十字绞杀合体技：钳+锯左右钳形、炮+激光上下封位，对角缝隙可走。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.CrossExecute, typeof(PrimeStateContext))]
    internal class PrimeCrossExecuteState : PrimeStateBase
    {
        public override string StateName => "CrossExecute";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.CrossExecute;

        private const int Telegraph = 36;
        private const int Execute = 90;
        private const int Total = Telegraph + Execute + 20;

        public override void OnEnter(PrimeStateContext context) {
            base.OnEnter(context);
            context.Npc.ai[PrimeAiSlots.HeadCommandSlot] = (float)PrimeCommandKind.CrossExecute;
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 0;

            Vector2 anchor = context.Target.Center;
            npc.velocity = Vector2.Lerp(npc.velocity, (anchor + new Vector2(0, -280) - npc.Center) * 0.05f, 0.12f);
            LeanTowards(npc, context.Target.Center);

            if (Timer < Telegraph) {
                context.SetChargeState(1, Timer / (float)Telegraph);
                //四条冲锋走廊预警：以玩家为中心的十字（左右钳形+上下封位），对角缝隙可走
                if (!VaultUtils.isClient && Timer == 1) {
                    for (int i = 0; i < 4; i++) {
                        PrimeTelegraphLine.SpawnLine(npc, context.Target.Center, MathHelper.PiOver2 * i, Telegraph);
                    }
                }
            }
            else if (Timer == Telegraph && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.8f, Pitch = 0.3f }, npc.Center);
            }

            Timer++;
            if (Timer >= Total && !VaultUtils.isClient) {
                npc.ai[PrimeAiSlots.HeadCommandSlot] = 0f;
                return new PrimeCommandSequenceState();
            }
            return null;
        }

        public override void OnExit(PrimeStateContext context) {
            base.OnExit(context);
            context.Npc.ai[PrimeAiSlots.HeadCommandSlot] = 0f;
        }
    }
}
