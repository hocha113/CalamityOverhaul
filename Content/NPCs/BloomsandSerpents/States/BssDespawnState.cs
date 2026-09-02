using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>脱战撤离：收腿钻沙，加速下潜遁走，深处清场</summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.Despawn, typeof(BssStateContext))]
    internal class BssDespawnState : BssStateBase
    {
        public override string StateName => "Despawn";
        public override BssStateIndex StateIndex => BssStateIndex.Despawn;

        private float prevY;
        private bool diveFxDone;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            prevY = ctx.Npc.Center.Y;
            diveFxDone = false;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            ctx.LegCommand = BssLegCommand.Tuck;
            ctx.LegAlpha = MathHelper.Clamp(1f - t / 14f, 0f, 1f);
            ctx.Mode = BssMoveMode.Direct;
            DeclareJaw(ctx, BssJawCommand.Clamp);

            if (t < 14) {
                npc.velocity *= 0.9f;
            }
            else {
                npc.velocity.X *= 0.99f;
                npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y + 0.55f, -10f, 30f);
                npc.alpha = Math.Min(npc.alpha + 5, 255);
            }

            //入土表现
            if (!diveFxDone && !Main.dedServ && t >= 14) {
                float groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 300f), 900f);
                if (prevY < groundY && npc.Center.Y >= groundY - 10f) {
                    diveFxDone = true;
                    BssVfx.SandBurst(new Vector2(npc.Center.X, groundY), 1.3f);
                }
            }
            prevY = npc.Center.Y;

            Timer++;

            //深处清场（权威端）
            if (!VaultUtils.isClient && (t > 150 || (t > 40 && npc.alpha >= 250))) {
                BssHead.HandleDespawnAll();
            }
            return null;
        }
    }
}
