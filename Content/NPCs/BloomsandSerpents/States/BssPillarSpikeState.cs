using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 沙柱突刺（全场怒放）：立起跺地锁定花心（跺地拍即预告主体 + 承诺）→
    /// 以花心为序、0/+1/-1/+2/-2 向两翼快节奏滚开点名 12~16 根巨柱——鼓包波
    /// 先扫过全场、柱群按同序轰起，一次释放全场沸腾。柱滞留成为腾跃/爆震的燃料。
    /// 公平口径：花心在跺地帧锁死（波形是承诺的固定图案，不追人）；每根先顶
    /// SpikeOmenFrames 帧鼓包（脚下鼓包即警报），伤害窗只在钻出的 9 帧
    /// （伤害窗 = 可见冲势）；车道间距 − 抖散 ≥ 走廊宽 = 柱间走廊是声明的逃生道。
    /// 高飞玩家自动走空中凝沙变体（柱在空中凝出，反制飞天）。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.PillarSpike, typeof(BssStateContext))]
    internal class BssPillarSpikeState : BssStateBase
    {
        public override string StateName => "PillarSpike";
        public override BssStateIndex StateIndex => BssStateIndex.PillarSpike;

        private enum SpikePhase
        {
            Stomp,  //立起跺地蓄势（锁花心）
            Call,   //怒放波逐根点名
            Settle, //收势一拍
        }

        private SpikePhase phase;
        /// <summary>已点名根数</summary>
        private int called;
        /// <summary>怒放花心（跺地帧锁定，波形即承诺）</summary>
        private Vector2 bloomCenter;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phase = SpikePhase.Stomp;
            called = 0;
            bloomCenter = ctx.Target.Center;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case SpikePhase.Stomp:
                    UpdateStomp(ctx, npc);
                    break;
                case SpikePhase.Call:
                    UpdateCall(ctx, npc);
                    break;
                case SpikePhase.Settle:
                    ctx.Mode = BssMoveMode.Crawl;
                    ctx.CrawlDirX = FacingToTarget(ctx);
                    ctx.CrawlSpeed = BssDirector.CrawlCruiseSpeed;
                    ctx.LegCommand = BssLegCommand.March;
                    Timer++;
                    if (Timer > 16) {
                        return EndAttack(ctx);
                    }
                    break;
            }

            //超时保险兜底
            if (Counter++ > 60 * 6) {
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(SpikePhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>立起跺地：前身昂起收折 → 落拍跺地（全髋下沉 + 鞭波 + 沙爆），跺地即开始点名</summary>
        private void UpdateStomp(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = 2f;
            ctx.LegCommand = BssLegCommand.Raise;
            ctx.FrontRaise = MathHelper.Clamp(t / 14f, 0f, 1f);
            ctx.Compression = Math.Min(ctx.Compression, 0.94f);
            DeclareJaw(ctx, BssJawCommand.Inhale, ctx.FrontRaise);

            if (t == 2 && !Main.dedServ) {
                BssVfx.Roar(npc.Center, -0.2f, 0.7f);
            }
            //末段绷紧亮花（点名将至）
            if (t > BssDirector.SpikeStompFrames - 8) {
                ctx.BloomGlow = Math.Max(ctx.BloomGlow, 0.8f);
                if (!Main.dedServ) {
                    npc.position += Main.rand.NextVector2Circular(1.2f, 1.2f);
                }
            }

            Timer++;
            if (t >= BssDirector.SpikeStompFrames) {
                //跺地拍：力量在落拍帧；花心此刻锁死（波形即承诺）
                bloomCenter = ctx.Target.Alives() ? ctx.Target.Center : npc.Center;
                ctx.PulseWhip(8f);
                ctx.PulseGapWave(SerpentChainMath.WavePress, 0.12f);
                for (int k = 0; k < ctx.StationBob.Length; k++) {
                    ctx.StationBob[k] = 1.1f;
                }
                if (!Main.dedServ) {
                    float groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 60f));
                    BssVfx.SandBurst(new Vector2(npc.Center.X, groundY), 1.4f);
                    BssVfx.Roar(npc.Center, -0.45f, 1.05f);
                    BssVfx.Shake(npc.Center, 6f, 1400f);
                }
                SwitchPhase(SpikePhase.Call);
            }
        }

        /// <summary>
        /// 怒放波点名：每 SpikeStepGap 帧按 0/+1/-1/+2/-2 扩散序召一根武装柱
        /// （蛇不站桩，继续爬行压迫）；鼓包波从花心向两翼扫过全场、柱群按同序
        /// 轰起 = 一次释放全场沸腾。点名拍亮花 + 顿一记（boss 与柱的因果读数）。
        /// </summary>
        private void UpdateCall(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = 9f;
            ctx.LegCommand = BssLegCommand.March;
            DeclareRoarHold(ctx, t, 20);

            int total = BssDirector.SpikeCount(ctx.Phase);
            if (t % BssDirector.SpikeStepGap == 0 && called < total) {
                //点名拍：亮花 + 顿挫；波中段持续微震（全场沸腾的地鸣）
                ctx.BloomGlow = 1f;
                if (called % 4 == 0) {
                    ctx.PulseGapWave(SerpentChainMath.WavePress, 0.07f);
                }
                if (!Main.dedServ) {
                    BssVfx.Shake(npc.Center, 1.6f, 1600f);
                }
                if (!VaultUtils.isClient) {
                    CallPillar(npc, called);
                }
                called++;
            }

            Timer++;
            if (called >= total && t > (called - 1) * BssDirector.SpikeStepGap + 14) {
                SwitchPhase(SpikePhase.Settle);
            }
        }

        /// <summary>
        /// 权威端落点解算：花心 + 扩散序车道（0/+1/-1/+2/-2...）+ 微抖散，
        /// 与既有柱保持最小间距（柱间走廊 = 声明的逃生道）。
        /// </summary>
        private void CallPillar(NPC npc, int slot) {
            //扩散序：0, +1, -1, +2, -2...（花心先起，向两翼滚开）
            int lane = slot == 0 ? 0 : (slot + 1) / 2 * ((slot & 1) == 1 ? 1 : -1);
            float x = bloomCenter.X + lane * BssDirector.SpikeLaneSpacing
                + Main.rand.NextFloat(-BssDirector.SpikeScatterPx, BssDirector.SpikeScatterPx);

            //间距钳制：离最近既有柱太近就沿本翼向外推一步（别把走廊堵死）
            float push = lane != 0 ? Math.Sign(lane) : 1f;
            foreach (var pillar in BssSandPillar.Alive) {
                if (Math.Abs(x - pillar.CenterX) < BssDirector.SpikeMinGapPx) {
                    float side = Math.Sign(x - pillar.CenterX);
                    if (side == 0f) {
                        side = push;
                    }
                    x = pillar.CenterX + side * BssDirector.SpikeMinGapPx;
                }
            }

            float height = Main.rand.NextFloat(BssDirector.PillarHeightMin, BssDirector.PillarHeightMax);
            BssSandPillar.Spawn(npc, new Vector2(x, bloomCenter.Y), height,
                BssDirector.PillarWidth, BssDirector.SpikeOmenFrames,
                BssDirector.PillarSpikeLinger, armedPillar: true);
        }
    }
}
