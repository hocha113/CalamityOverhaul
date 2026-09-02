using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 沙柱爆震：蛇后仰立起怒吼（声波环屏效 + 沙暴脉冲 + 震屏），场上全部沙柱
    /// 被点名进入裂纹预闪，随后按离蛇距离近→远错拍逐柱炸成径向沙球环。
    /// 柱不足时先在玩家两翼种应急柱（种柱本身即预告）。
    /// 公平口径：怒吼期蛇钉在原地立起 = 白给输出窗；每柱裂纹预闪 ≥30 帧 +
    /// 逐柱错拍 = 波次可读；球环缺口 + 柱间走廊 = 声明的逃生道。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.PillarBurst, typeof(BssStateContext))]
    internal class BssPillarBurstState : BssStateBase
    {
        public override string StateName => "PillarBurst";
        public override BssStateIndex StateIndex => BssStateIndex.PillarBurst;

        private enum BurstPhase
        {
            Seed,  //柱不足先种应急柱
            Roar,  //后仰怒吼（声波环 + 点名全柱）
            Watch, //收势看爆（柱自走引信）
        }

        private BurstPhase phase;
        /// <summary>怒吼立起锚（X 定桩不漂）</summary>
        private Vector2 roarAnchor;
        /// <summary>权威端已种应急柱</summary>
        private bool seeded;
        /// <summary>怒吼拍帧号（进入 Roar 后第几帧点火）</summary>
        private const int RoarBeat = 22;
        /// <summary>收势看爆时长</summary>
        private const int WatchFrames = 86;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            seeded = false;
            phase = BssSandPillar.CountDetonatable() >= BssDirector.BurstMinPillars
                ? BurstPhase.Roar : BurstPhase.Seed;
            roarAnchor = ctx.Npc.Center;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case BurstPhase.Seed:
                    UpdateSeed(ctx, npc);
                    break;
                case BurstPhase.Roar:
                    UpdateRoar(ctx, npc);
                    break;
                case BurstPhase.Watch:
                    UpdateWatch(ctx, npc);
                    if (Timer > WatchFrames) {
                        return EndAttack(ctx);
                    }
                    break;
            }

            //超时保险兜底
            if (Counter++ > 60 * 7) {
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(BurstPhase next, BssStateContext ctx) {
            phase = next;
            Timer = 0;
            if (next == BurstPhase.Roar) {
                roarAnchor = ctx.Npc.Center;
            }
        }

        /// <summary>种应急柱：玩家两翼各一根无伤害柱（种柱即预告），升起可点名即入怒吼</summary>
        private void UpdateSeed(BssStateContext ctx, NPC npc) {
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = BssDirector.CrawlCruiseSpeed;
            ctx.LegCommand = BssLegCommand.March;

            if (!VaultUtils.isClient && !seeded && ctx.Target.Alives()) {
                seeded = true;
                for (int i = 0; i < BssDirector.BurstFallbackPillars; i++) {
                    //两翼交替、半径递增（-340, +340, -590, +590...），不同点叠柱
                    float offset = (i % 2 == 0 ? -1f : 1f)
                        * (340f + i / 2 * BssDirector.SpikeLaneSpacing * 1.2f);
                    Vector2 anchor = ctx.Target.Center + new Vector2(offset, 0f);
                    BssSandPillar.Spawn(npc, anchor,
                        Main.rand.NextFloat(BssDirector.PillarHeightMin, BssDirector.PillarHeightMax),
                        BssDirector.PillarWidth, 18, BssDirector.PillarSpikeLinger, armedPillar: false);
                }
            }

            Timer++;
            //柱升起即入怒吼；种柱失败（探地被拒等）超时放弃本招
            if (BssSandPillar.CountDetonatable() >= 1 && Timer > 34) {
                SwitchPhase(BurstPhase.Roar, ctx);
            }
            else if (Timer > 90) {
                Counter = 60 * 7 + 1;
            }
        }

        /// <summary>
        /// 后仰怒吼：钉桩立起（白给输出窗 = 公平阀），怒吼拍点火声波环 + 沙暴脉冲，
        /// 同帧权威端按近→远错拍点名全部柱进入裂纹预闪。
        /// </summary>
        private void UpdateRoar(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            float raise = MathHelper.Clamp(t / (float)RoarBeat, 0f, 1f);

            //钉桩立起：X 定在锚点，前身昂起
            float groundY = BssVfx.FindGroundY(new Vector2(roarAnchor.X, roarAnchor.Y - 200f));
            Vector2 pose = new(roarAnchor.X, groundY - BssDirector.CrawlRideHeight - 150f * raise);
            ctx.Mode = BssMoveMode.Direct;
            npc.velocity = Vector2.Lerp(npc.velocity, (pose - npc.Center) * 0.1f, 0.3f);

            ctx.LegCommand = BssLegCommand.Raise;
            ctx.FrontRaise = raise;
            ctx.Compression = Math.Min(ctx.Compression, 0.9f);
            ctx.BloomGlow = Math.Max(ctx.BloomGlow, raise * 0.9f);
            if (t < RoarBeat) {
                DeclareJaw(ctx, BssJawCommand.Inhale, raise);
            }
            else {
                DeclareRoarHold(ctx, t - RoarBeat);
            }

            //昂首：头指向斜上方（吼向天）
            float toward = FacingToTarget(ctx, 0f);
            float skyAng = new Vector2(toward * 0.35f, -1f).ToRotation();
            npc.rotation = npc.rotation.AngleLerp(skyAng + BssHead.FacingRot, 0.16f);

            //末段绷紧
            if (raise > 0.7f && !Main.dedServ) {
                npc.position += Main.rand.NextVector2Circular(1.5f, 1.5f);
            }

            if (t == RoarBeat) {
                RoarBeatFire(ctx, npc);
            }

            Timer++;
            if (t >= BssDirector.BurstRoarFrames) {
                SwitchPhase(BurstPhase.Watch, ctx);
            }
        }

        /// <summary>怒吼拍：声波环 + 深吼 + 震屏 + 沙暴脉冲（各端本地）；权威端点名全柱错拍引爆</summary>
        private void RoarBeatFire(BssStateContext ctx, NPC npc) {
            ctx.FireRoarRing(npc.Center);
            ctx.PulseGapWave(SerpentChainMath.WavePress, 0.14f);
            ctx.PulseWhip(9f);
            ctx.StormLevel = Math.Min(ctx.StormLevel + 0.3f, 1f);
            if (!Main.dedServ) {
                BssVfx.Roar(npc.Center, -0.65f, 1.25f);
                BssVfx.Shake(npc.Center, 9f, 1700f);
            }

            if (VaultUtils.isClient) {
                return;
            }
            //近柱先爆：距离排序后错拍点名（波次可读）
            List<BssSandPillar> targets = new();
            foreach (var pillar in BssSandPillar.Alive) {
                if (pillar.Detonatable) {
                    targets.Add(pillar);
                }
            }
            targets.Sort((a, b) =>
                Math.Abs(a.CenterX - npc.Center.X).CompareTo(Math.Abs(b.CenterX - npc.Center.X)));
            for (int i = 0; i < targets.Count; i++) {
                targets[i].CommandDetonate(BssDirector.BurstCrackFrames, i * BssDirector.BurstStaggerGap);
            }
        }

        /// <summary>收势看爆：立姿缓收、慢爬压迫，引爆由柱自走（蛇不站桩干等）</summary>
        private void UpdateWatch(BssStateContext ctx, NPC npc) {
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = 7f;
            ctx.LegCommand = BssLegCommand.March;
            ctx.FrontRaise *= 0.9f;
            Timer++;
        }
    }
}
