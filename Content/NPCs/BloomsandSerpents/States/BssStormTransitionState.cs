using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 沙暴加剧转阶段（60%）：清弹 → 后腿+尾锚地、前身高高立起 → 怒吼（声波环）把入场
    /// 就有的沙暴催到满级、全身红花齐颤落瓣 → 落回巡曳。
    /// 全程无伤害；收招挂冷却 = 转阶段后的攻速热身阀。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.StormTransition, typeof(BssStateContext))]
    internal class BssStormTransitionState : BssStateBase
    {
        public override string StateName => "StormTransition";
        public override BssStateIndex StateIndex => BssStateIndex.StormTransition;

        private const int HaltFrames = 16;
        private const int RoarFrame = 54;
        private const int SettleFrom = 76;
        private const int EndFrame = 96;

        private Vector2 anchor;
        private float groundY;
        private bool roared;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            anchor = ctx.Npc.Center;
            groundY = BssVfx.FindGroundY(anchor - new Vector2(0f, 60f));
            roared = false;
            ctx.RefreshSegments();
            //公平阀：转阶段清掉自家全部敌对弹幕
            BssVfx.ClearOwnHostileProjectiles();
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            ctx.Mode = BssMoveMode.Direct;
            ctx.Compression = 0.88f;

            //立起进度：怒吼前爬升，收势期回落
            float raise;
            if (t < HaltFrames) {
                raise = 0f;
                npc.velocity *= 0.85f;
            }
            else if (t < SettleFrom) {
                raise = MathHelper.Clamp((t - HaltFrames) / (float)(RoarFrame - HaltFrames), 0f, 1f);
                raise = raise * raise * (3f - 2f * raise);
            }
            else {
                raise = MathHelper.Clamp(1f - (t - SettleFrom) / 26f, 0f, 1f);
            }

            ctx.LegCommand = raise > 0.25f ? BssLegCommand.Raise : BssLegCommand.March;
            ctx.FrontRaise = raise;
            if (t >= HaltFrames && t < RoarFrame) {
                DeclareJaw(ctx, BssJawCommand.Inhale, raise);
            }
            else if (t >= RoarFrame) {
                DeclareRoarHold(ctx, t - RoarFrame, 28);
            }

            if (t >= HaltFrames) {
                Vector2 pose = new(anchor.X, groundY - BssDirector.CrawlRideHeight - 210f * raise);
                Vector2 desired = (pose - npc.Center) * 0.08f;
                if (desired.Length() > 8f) {
                    desired = desired.SafeNormalize(Vector2.Zero) * 8f;
                }
                npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.18f);
                npc.rotation = npc.rotation.AngleLerp(new Vector2(0.15f * ctx.WindSign, -1f).ToRotation() + BssHead.FacingRot, 0.12f);
            }

            //爬升期的隆隆与渗沙
            if (t >= HaltFrames && t < RoarFrame && !Main.dedServ) {
                if (t % 10 == 0) {
                    BssVfx.Shake(npc.Center, 1.5f + 3f * raise, 1100f);
                }
                BssVfx.SandTrickle(npc.Center + Main.rand.NextVector2Circular(30f, 40f), 1f + raise);
            }

            //怒吼拍：沙暴催满（转阶段的大震拍）
            if (t >= RoarFrame && !roared) {
                roared = true;
                ctx.Phase = 2;
                ctx.FireRoarRing(npc.Center);
                if (!Main.dedServ) {
                    BssVfx.Roar(npc.Center, -0.6f, 1.2f);
                    BssVfx.Roar(npc.Center, -0.1f, 0.8f);
                    BssVfx.Shake(npc.Center, 12f, 1600f);
                    //全身红花齐颤落瓣
                    foreach (var seg in ctx.Segments) {
                        if (!seg.Alives() || !BssStateContext.IsFlowerOrdinal((int)seg.ai[0])) {
                            continue;
                        }
                        for (int i = 0; i < 3; i++) {
                            BssVfx.PetalDrift(seg.Center + Main.rand.NextVector2Circular(12f, 12f),
                                new Vector2(ctx.WindSign * Main.rand.NextFloat(1f, 2.6f), -Main.rand.NextFloat(0.5f, 2f)));
                        }
                    }
                }
            }
            if (roared) {
                //沙暴催满（此后由头部的阶段底线保持）
                ctx.StormLevel = Math.Max(ctx.StormLevel, MathHelper.Clamp(0.9f + (t - RoarFrame) / 46f * 0.1f, 0f, 1f));
                ctx.PulseKind = 4;
                ctx.BloomGlow = Math.Max(ctx.BloomGlow, 0.8f * MathHelper.Clamp(1.4f - (t - RoarFrame) / 40f, 0f, 1f));
            }

            Timer++;

            if (t > EndFrame || t > 160) {
                //转阶段后的热身阀：回巡曳缓一口气再出招，P2 首手是轮换表的掠冲
                ctx.AttackCooldown = 40;
                return new BssHubState();
            }
            return null;
        }
    }
}
