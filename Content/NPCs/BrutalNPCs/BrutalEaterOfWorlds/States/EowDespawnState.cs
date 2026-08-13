using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.States
{
    /// <summary>撤离：一头扎回地底，尘土落定后除名</summary>
    [InnoVault.StateMachines.VaultState((int)EowStateIndex.Despawn, typeof(EowStateContext))]
    internal class EowDespawnState : EowStateBase
    {
        public override string StateName => "Despawn";
        public override EowStateIndex StateIndex => EowStateIndex.Despawn;
        public override bool AllowFarSnap => false;

        private bool groundPassed;
        private float groundY;

        public EowDespawnState() {
        }

        public override void OnEnter(EowStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            groundPassed = false;
            groundY = EowMotionFX.FindGroundBelow(context.Npc.Center).Y;
            EowMotionFX.PlayRoar(context.Npc.Center, -0.7f, 0.8f);
        }

        public override IEowState OnUpdate(EowStateContext context) {
            NPC npc = context.Npc;
            npc.dontTakeDamage = true;
            npc.damage = 0;

            //俯冲入地
            npc.velocity.X *= 0.97f;
            npc.velocity.Y = MathHelper.Lerp(npc.velocity.Y, 46f, 0.06f);
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            //穿地尘爆
            if (!groundPassed && npc.Center.Y > groundY) {
                groundPassed = true;
                EowMotionFX.SpawnDirtBurst(new Vector2(npc.Center.X, groundY), 1.5f);
            }

            Tick();
            if (Timer > 150 && !VaultUtils.isClient) {
                EowHeadAI.HandleDespawnAll();
            }

            return null;
        }
    }
}
