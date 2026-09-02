using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 死亡演出（约 5 秒）：踉跄失速 → 四腿逐只失力跪倒（每倒一腿一记闷响）→
    /// 瘫贴地面 → 繁花回光返照 → 静默一拍 → 溃爆波尾到头扫过 → 头垂落结算。
    /// 全程锁血无伤害；沙暴随之退场。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.Death, typeof(BssStateContext))]
    internal class BssDeathState : BssStateBase
    {
        public override string StateName => "Death";
        public override BssStateIndex StateIndex => BssStateIndex.Death;

        private const int StumbleEnd = 40;
        private const int CollapseEnd = 132;
        private const int FlareEnd = 168;
        private const int QuietEnd = 178;
        private const int RuptureFrames = 116;
        private const int FinaleFrame = QuietEnd + RuptureFrames;

        private int prevCollapsed;
        private bool finaleDone;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            prevCollapsed = 0;
            finaleDone = false;
            ctx.RefreshSegments();
            BssVfx.ClearOwnHostileProjectiles();
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            //演出锁血
            npc.dontTakeDamage = true;
            if (npc.life < 1) {
                npc.life = 1;
            }
            //沙暴退场
            ctx.StormLevel = Math.Max(0f, ctx.StormLevel - 0.008f);
            ctx.LegAlpha = 1f;
            DeclareJaw(ctx, BssJawCommand.Slack);

            float groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 60f));

            if (t < StumbleEnd) {
                //踉跄：爬速指数塌陷，脚步声变闷
                ctx.Mode = BssMoveMode.Crawl;
                ctx.CrawlDirX = ctx.CrawlDirX != 0f ? ctx.CrawlDirX : 1f;
                ctx.CrawlSpeed = 4.5f * (1f - t / (float)StumbleEnd);
                ctx.LegCommand = BssLegCommand.March;
                if (t % 13 == 0 && !Main.dedServ) {
                    BssVfx.Roar(npc.Center, -0.85f, 0.5f);
                    BssVfx.SandTrickle(npc.Center, 1.2f);
                }
            }
            else if (t < CollapseEnd) {
                //逐腿失力：每 22 帧倒一条
                ctx.Mode = BssMoveMode.Direct;
                npc.velocity.X *= 0.9f;
                float sinkY = groundY - 16f;
                npc.velocity.Y = MathHelper.Clamp((sinkY - npc.Center.Y) * 0.05f, -2f, 2.4f);
                ctx.LegCommand = BssLegCommand.Collapse;
                ctx.CollapsedLegs = Math.Min(BssLegRig.LegCount, 1 + (t - StumbleEnd) / 22);
                if (ctx.CollapsedLegs != prevCollapsed) {
                    prevCollapsed = ctx.CollapsedLegs;
                    if (!Main.dedServ) {
                        Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Dig
                            with { Volume = 0.8f, Pitch = -0.7f, MaxInstances = 2 }, npc.Center);
                        BssVfx.SandBurst(npc.Bottom, 0.6f);
                        BssVfx.Shake(npc.Center, 3.2f, 1100f);
                    }
                }
            }
            else if (t < FlareEnd) {
                //回光返照：繁花齐亮
                HoldFlat(ctx, npc, groundY);
                ctx.PulseKind = 4;
                ctx.BloomGlow = Math.Max(ctx.BloomGlow, (t - CollapseEnd) / (float)(FlareEnd - CollapseEnd));
                if (t == CollapseEnd + 6 && !Main.dedServ) {
                    BssVfx.Roar(npc.Center, -0.4f, 0.8f);
                }
                if (t >= CollapseEnd + 6) {
                    DeclareRoarHold(ctx, t - (CollapseEnd + 6), 18);
                }
            }
            else if (t < QuietEnd) {
                //静默一拍：溃爆前的吸气
                HoldFlat(ctx, npc, groundY);
                DeclareJaw(ctx, BssJawCommand.Inhale,
                    MathHelper.Clamp((t - FlareEnd) / (float)Math.Max(QuietEnd - FlareEnd, 1), 0f, 1f));
            }
            else if (t < FinaleFrame) {
                //溃爆波：尾→头扫过，体节各自在波前经过时本地炸沙落瓣
                HoldFlat(ctx, npc, groundY);
                ctx.PulseKind = 3;
                ctx.PulsePhase = 1f - (t - QuietEnd) / (float)RuptureFrames;
                if (t % 16 == 0) {
                    BssVfx.Shake(npc.Center, 2f, 1200f);
                }
            }
            else if (!finaleDone) {
                //终幕：头爆沙散瓣，真死结算
                finaleDone = true;
                ctx.DeathPerformanceFinished = true;
                DeclareJaw(ctx, BssJawCommand.Roar, 1f);
                if (!Main.dedServ) {
                    BssVfx.SandBurst(npc.Center, 2f);
                    BssVfx.Roar(npc.Center, -1f, 1.2f);
                    BssVfx.Shake(npc.Center, 10f, 1600f);
                    for (int i = 0; i < 16; i++) {
                        BssVfx.PetalDrift(npc.Center + Main.rand.NextVector2Circular(34f, 24f),
                            Main.rand.NextVector2Circular(3f, 2.4f) - new Vector2(0f, 1f));
                    }
                }
                if (!VaultUtils.isClient) {
                    npc.life = 0;
                    npc.HitEffect();
                    npc.checkDead();
                    npc.netUpdate = true;
                }
            }

            Timer++;

            //超时保险：演出被卡也要能真死
            if (t > FinaleFrame + 90 && !ctx.DeathPerformanceFinished) {
                ctx.DeathPerformanceFinished = true;
                if (!VaultUtils.isClient) {
                    npc.life = 0;
                    npc.checkDead();
                }
            }
            return null;
        }

        /// <summary>瘫贴地面的持位</summary>
        private static void HoldFlat(BssStateContext ctx, NPC npc, float groundY) {
            ctx.Mode = BssMoveMode.Direct;
            npc.velocity.X *= 0.85f;
            npc.velocity.Y = MathHelper.Clamp((groundY - 14f - npc.Center.Y) * 0.05f, -2f, 2f);
            ctx.LegCommand = BssLegCommand.Collapse;
            ctx.CollapsedLegs = BssLegRig.LegCount;
        }
    }
}
