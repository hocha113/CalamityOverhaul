using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.States
{
    /// <summary>
    /// P3 转阶段「满溢怒放」（28% 血线，全程无伤）：短促收势 → 满身囊肿齐闪 →
    /// 怒吼宣言 Phase=3。终局宣言拍，收束即接满场引爆（P3 轮换首招）。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.Overflow, typeof(FssStateContext))]
    internal class FssOverflowState : FssStateBase
    {
        public override string StateName => "Overflow";
        public override FssStateIndex StateIndex => FssStateIndex.Overflow;

        private const int GatherEnd = 22;
        private const int FlashEnd = 54;
        private const int Timeout = 90;

        public override void OnEnter(FssStateContext ctx) {
            base.OnEnter(ctx);
            FssVfx.ClearOwnHostileProjectiles();
        }

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            if (t < GatherEnd) {
                //收势盘紧：走 Crawl 拉回地面（空中触发时 Direct 减速会悬停定桩，
                //接满场引爆的立腿剪影就悬在半空破功）
                ctx.Mode = FssMoveMode.Crawl;
                ctx.CrawlSpeed = 0f;
                ctx.CrawlDirX = FacingToTarget(ctx);
                ctx.Compression = Math.Min(ctx.Compression, 0.88f);
            }
            else if (t < FlashEnd) {
                //满身齐闪 + 怒吼宣言
                ctx.Mode = FssMoveMode.Crawl;
                ctx.CrawlSpeed = 0f;
                ctx.CrawlDirX = FacingToTarget(ctx);
                ctx.LegCommand = FssLegCommand.Raise;
                ctx.FrontRaise = MathHelper.Clamp((t - GatherEnd) / 14f, 0f, 0.8f);
                ctx.PulseKind = 4;
                ctx.CystGlow = 1f;
                if (t == GatherEnd + 8) {
                    ctx.Phase = 3;
                    if (!Main.dedServ) {
                        FssVfx.Roar(npc.Center, -0.5f, 1.2f);
                        FssVfx.IchorBurst(npc.Center, 2.2f, -Vector2.UnitY);
                        FssVfx.Shake(npc.Center, 6f, 1500f);
                    }
                }
            }
            else {
                //终局宣言收束即接满场引爆（P3 首招 = 全场池经济当场收账）
                ctx.Phase = 3;
                ctx.PostTransitionRamp = FssDirector.PostTransitionRampFrames;
                return new FssFieldDetonateState();
            }

            Timer++;
            if (t > Timeout) {
                ctx.Phase = 3;
                ctx.PostTransitionRamp = FssDirector.PostTransitionRampFrames;
                return new FssFieldDetonateState();
            }
            return null;
        }
    }
}
