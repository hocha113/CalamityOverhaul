using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 繁花怒放连接段（25%）：全花齐闪 + 怒吼，沙暴拉满，此后提速并解锁连击。
    /// 短促（约 1.5 秒），是终局宣言不是二次转阶段。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.ApexBloom, typeof(BssStateContext))]
    internal class BssApexBloomState : BssStateBase
    {
        public override string StateName => "ApexBloom";
        public override BssStateIndex StateIndex => BssStateIndex.ApexBloom;

        private const int RoarFrame = 14;
        private const int EndFrame = 44;
        private bool roared;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            roared = false;
            ctx.RefreshSegments();
            BssVfx.ClearOwnHostileProjectiles();
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlSpeed = 0f;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.LegCommand = BssLegCommand.Raise;
            ctx.FrontRaise = MathHelper.Clamp(t / 14f, 0f, 0.7f);
            ctx.Compression = 0.9f;

            if (t >= RoarFrame && !roared) {
                roared = true;
                ctx.Phase = 3;
                if (!Main.dedServ) {
                    BssVfx.Roar(npc.Center, 0.1f, 1.1f);
                    BssVfx.Shake(npc.Center, 8f, 1400f);
                    foreach (var seg in ctx.Segments) {
                        if (!seg.Alives() || !BssStateContext.IsFlowerOrdinal((int)seg.ai[0])) {
                            continue;
                        }
                        for (int i = 0; i < 4; i++) {
                            BssVfx.PetalDrift(seg.Center, Main.rand.NextVector2Circular(2.6f, 2f)
                                - new Vector2(0f, 1.5f));
                        }
                    }
                }
            }
            if (roared) {
                ctx.PulseKind = 4;
                ctx.BloomGlow = Math.Max(ctx.BloomGlow, 1f);
            }

            Timer++;

            if (t > EndFrame || t > 80) {
                //热身阀：怒放后的第一招也别贴脸即到
                ctx.AttackCooldown = 26;
                return new BssHubState();
            }
            return null;
        }
    }
}
