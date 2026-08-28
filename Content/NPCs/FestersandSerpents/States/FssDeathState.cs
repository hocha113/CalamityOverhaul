using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.States
{
    /// <summary>
    /// 死亡演出「溃解」（约 6 秒）：踉跄失速 → 四腿逐站失力跪倒 → 满身囊肿回光齐亮 →
    /// 静默一拍 → 溃爆波尾到头加速扫过（囊肿链爆、体节瘪暗、侵蚀渐深）→
    /// 头最后昂起欲吼却哽住（音频剪断）→ 倒伏，尸躯喷终场灵液泉，真死结算。
    /// 全程锁血无伤害；腐沙暴随之退场。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.Death, typeof(FssStateContext))]
    internal class FssDeathState : FssStateBase
    {
        public override string StateName => "Death";
        public override FssStateIndex StateIndex => FssStateIndex.Death;

        private const int StumbleEnd = 40;
        private const int CollapseEnd = 132;
        private const int FlareEnd = 170;
        private const int QuietEnd = 180;
        private const int RuptureFrames = 128;
        /// <summary>昂首欲吼段起点（波扫完）</summary>
        private const int ChokeStart = QuietEnd + RuptureFrames;
        private const int ChokeFrames = 40;
        private const int FinaleFrame = ChokeStart + ChokeFrames;

        private int prevCollapsed;
        private bool finaleDone;
        private bool chokeCued;

        public override void OnEnter(FssStateContext ctx) {
            base.OnEnter(ctx);
            prevCollapsed = 0;
            finaleDone = false;
            chokeCued = false;
            ctx.RefreshSegments();
            FssVfx.ClearOwnHostileProjectiles();
        }

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            //演出锁血
            npc.dontTakeDamage = true;
            if (npc.life < 1) {
                npc.life = 1;
            }
            //腐沙暴退场
            ctx.StormLevel = Math.Max(0f, ctx.StormLevel - 0.007f);
            ctx.LegAlpha = 1f;

            float groundY = FssVfx.FindGroundY(npc.Center - new Vector2(0f, 60f));

            if (t < StumbleEnd) {
                //踉跄：爬速指数塌陷，湿咳低鸣
                ctx.Mode = FssMoveMode.Crawl;
                ctx.CrawlDirX = ctx.CrawlDirX != 0f ? ctx.CrawlDirX : 1f;
                ctx.CrawlSpeed = 4.5f * (1f - t / (float)StumbleEnd);
                ctx.LegCommand = FssLegCommand.March;
                if (t % 13 == 0 && !Main.dedServ) {
                    FssVfx.Roar(npc.Center, -0.95f, 0.45f);
                    FssVfx.FesterTrickle(npc.Center, 1.4f);
                }
            }
            else if (t < CollapseEnd) {
                //逐腿失力：每 22 帧倒一站
                ctx.Mode = FssMoveMode.Direct;
                npc.velocity.X *= 0.9f;
                float sinkY = groundY - 18f;
                npc.velocity.Y = MathHelper.Clamp((sinkY - npc.Center.Y) * 0.05f, -2f, 2.4f);
                ctx.LegCommand = FssLegCommand.Collapse;
                ctx.CollapsedLegs = Math.Min(FssLegRig.LegCount, 1 + (t - StumbleEnd) / 22);
                if (ctx.CollapsedLegs != prevCollapsed) {
                    prevCollapsed = ctx.CollapsedLegs;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.8f, Pitch = -0.7f, MaxInstances = 2 }, npc.Center);
                        FssVfx.CorruptSandBurst(npc.Bottom, 0.6f);
                        FssVfx.Shake(npc.Center, 3.4f, 1100f);
                    }
                }
            }
            else if (t < FlareEnd) {
                //回光返照：满身囊肿齐亮（金）
                HoldFlat(ctx, npc, groundY);
                ctx.PulseKind = 4;
                ctx.CystGlow = Math.Max(ctx.CystGlow, (t - CollapseEnd) / (float)(FlareEnd - CollapseEnd));
                if (t == CollapseEnd + 6 && !Main.dedServ) {
                    FssVfx.Roar(npc.Center, -0.5f, 0.75f);
                }
            }
            else if (t < QuietEnd) {
                //静默一拍：溃爆前的吸气
                HoldFlat(ctx, npc, groundY);
            }
            else if (t < ChokeStart) {
                //溃爆波：尾→头加速扫过，体节各自在波前经过时本地链爆；侵蚀渐深
                HoldFlat(ctx, npc, groundY);
                ctx.PulseKind = 3;
                float w = (t - QuietEnd) / (float)RuptureFrames;
                //加速波前：先慢后快（w 平方推进）
                ctx.PulsePhase = 1f - w * w;
                ctx.ErodeLevel = Math.Max(ctx.ErodeLevel, w * 0.85f);
                if (t % 14 == 0) {
                    FssVfx.Shake(npc.Center, 2.2f, 1200f);
                }
            }
            else if (t < FinaleFrame) {
                //昂首欲吼却哽住：头抬起、嘶声起半拍即剪断（死物最后的空鸣）；
                //残余脓池向尸体倒流汇聚——它吐出去的灵液回来收殓它
                HoldFlat(ctx, npc, groundY);
                ctx.PulseKind = 3;
                ctx.PulsePhase = 0f;
                ctx.ErodeLevel = Math.Max(ctx.ErodeLevel, 0.9f);
                float lift = MathF.Sin((t - ChokeStart) / (float)ChokeFrames * MathHelper.Pi);
                npc.velocity.Y = -lift * 1.6f;
                if (!chokeCued && t >= ChokeStart + 10) {
                    chokeCued = true;
                    DrainPools();
                    if (!Main.dedServ) {
                        //吼声起于半音量、瞬时哽断（湿息收尾不给完整吼）
                        SoundEngine.PlaySound(CalamityOverhaul.Common.CWRSound.SendRoar
                            with { Volume = 0.5f, Pitch = -0.9f, MaxInstances = 1 }, npc.Center);
                        SoundEngine.PlaySound(SoundID.NPCDeath13
                            with { Volume = 0.9f, Pitch = -0.6f, MaxInstances = 2 }, npc.Center);
                    }
                }
                UpdatePoolConvergence(npc);
            }
            else if (!finaleDone) {
                //终幕：尸躯喷终场灵液泉，真死结算
                finaleDone = true;
                ctx.DeathPerformanceFinished = true;
                if (!Main.dedServ) {
                    FssVfx.CorruptSandBurst(npc.Center, 2.2f);
                    FssVfx.IchorBurst(npc.Center, 2.6f, -Vector2.UnitY);
                    FssVfx.Shake(npc.Center, 10f, 1600f);
                    for (int i = 0; i < 12; i++) {
                        Dust gold = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(30f, 22f),
                            DustID.Ichor,
                            new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(4f, 11f)),
                            30, default, Main.rand.NextFloat(1f, 1.6f));
                        gold.noGravity = false;
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
        private static void HoldFlat(FssStateContext ctx, NPC npc, float groundY) {
            ctx.Mode = FssMoveMode.Direct;
            npc.velocity.X *= 0.85f;
            npc.velocity.Y = MathHelper.Clamp((groundY - 16f - npc.Center.Y) * 0.05f, -2f, 2f);
            ctx.LegCommand = FssLegCommand.Collapse;
            ctx.CollapsedLegs = FssLegRig.LegCount;
        }

        /// <summary>残余脓池提前入干涸段（权威端；伤害随干涸自关）</summary>
        private static void DrainPools() {
            if (VaultUtils.isClient) {
                return;
            }
            int type = Terraria.ModLoader.ModContent.ProjectileType<Projectiles.FssIchorPool>();
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type == type && p.timeLeft > 70) {
                    p.timeLeft = 70;
                    p.netUpdate = true;
                }
            }
        }

        /// <summary>脓池向尸体倒流的金流粒子（客户端，向心汇聚语法）</summary>
        private static void UpdatePoolConvergence(NPC npc) {
            if (Main.dedServ) {
                return;
            }
            int type = Terraria.ModLoader.ModContent.ProjectileType<Projectiles.FssIchorPool>();
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type != type || !Main.rand.NextBool(2)) {
                    continue;
                }
                Vector2 from = p.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), 0f);
                Dust flow = Dust.NewDustPerfect(from, DustID.IchorTorch,
                    (npc.Center - from) * 0.055f, 0, default, Main.rand.NextFloat(0.8f, 1.3f));
                flow.noGravity = true;
            }
        }
    }
}
