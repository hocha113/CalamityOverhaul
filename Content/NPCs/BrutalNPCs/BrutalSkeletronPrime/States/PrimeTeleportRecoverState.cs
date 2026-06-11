using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 传送恢复：机械风暴落幕、头部被传送至领域中心后的整备期。
    /// 机体缓缓上浮、四肢收拢，不造成任何伤害，给玩家一段确定的喘息窗口。
    /// 计时器由 <see cref="Projectiles.Boss.SkeletronPrime.SetPosingStarm.OnKill"/> 写入 override ai[10]。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.TeleportRecover, typeof(PrimeStateContext))]
    internal class PrimeTeleportRecoverState : PrimeStateBase
    {
        public override string StateName => "TeleportRecover";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.TeleportRecover;

        public override void OnEnter(PrimeStateContext context) {
            base.OnEnter(context);
            context.Npc.velocity = new Vector2(0, -6);
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 0;

            if (Timer < 30) {
                npc.velocity *= 0.98f;
            }
            else {
                npc.ChasingBehavior(context.Target.Center + new Vector2(0, -300), 20);
            }
            LeanByVelocity(npc);

            Timer++;
            if (context.TeleportTimer > 0) {
                context.TeleportTimer--;
            }

            if (context.TeleportTimer <= 0) {
                npc.damage = npc.defDamage * (context.NoArm ? 2 : 1);
                if (!VaultUtils.isClient) {
                    return npc.ai[PrimeAiSlots.HeadPhase] >= PrimePhase.Rage
                        ? new PrimeRageHoverState()
                        : new PrimeCommandHoverState();
                }
            }
            return null;
        }
    }
}
