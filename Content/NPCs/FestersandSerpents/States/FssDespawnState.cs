using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.States
{
    /// <summary>脱战撤离：收腿钻沙，加速下潜遁走，深处清场</summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.Despawn, typeof(FssStateContext))]
    internal class FssDespawnState : FssStateBase
    {
        public override string StateName => "Despawn";
        public override FssStateIndex StateIndex => FssStateIndex.Despawn;

        private float prevY;
        private bool diveFxDone;

        public override void OnEnter(FssStateContext ctx) {
            base.OnEnter(ctx);
            prevY = ctx.Npc.Center.Y;
            diveFxDone = false;
        }

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            ctx.LegCommand = FssLegCommand.Tuck;
            ctx.LegAlpha = MathHelper.Clamp(1f - t / 14f, 0f, 1f);
            ctx.Mode = FssMoveMode.Direct;

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
                float groundY = FssVfx.FindGroundY(npc.Center - new Vector2(0f, 300f), 900f);
                if (prevY < groundY && npc.Center.Y >= groundY - 10f) {
                    diveFxDone = true;
                    FssVfx.CorruptSandBurst(new Vector2(npc.Center.X, groundY), 1.4f);
                }
            }
            prevY = npc.Center.Y;

            Timer++;

            //深处清场（权威端）
            if (!VaultUtils.isClient && (t > 150 || (t > 40 && npc.alpha >= 250))) {
                FssHead.HandleDespawnAll();
            }
            return null;
        }
    }
}
