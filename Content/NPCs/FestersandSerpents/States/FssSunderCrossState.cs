using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.States
{
    /// <summary>
    /// A11 裂躯交叉（P3 压轴，地形无关）：立身怒吼，从链中段囊肿缝撕开——
    /// 后半身以缝节为临时首领独立成第二条蛇。两半分赴玩家两侧对角锚点，
    /// 同帧交叉冲刺（冲线近正交，在玩家位交汇成 X），换边再冲，随后后半
    /// 领节贴回前半尾节焊合归一。
    /// 公平口径：双向同时预亮 + 共享提示音、各自锁向 SunderLockLead 帧（承诺）、
    /// 冲线近正交 = 四个象限即声明逃生区、伤害窗全程速度门控。
    /// 健壮性：OnExit 无条件清 SplitLeaderOrdinal——死亡/超时任何路径都必然重连链。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.SunderCross, typeof(FssStateContext))]
    internal class FssSunderCrossState : FssStateBase
    {
        public override string StateName => "SunderCross";
        public override FssStateIndex StateIndex => FssStateIndex.SunderCross;

        private enum Phase { Tear, Regroup, Windup, CrossDash, Brake, Merge }

        private Phase phase;
        private int phaseTimer;
        /// <summary>撕裂缝链序（囊肿节，OnEnter 定死）</summary>
        private int seamOrdinal = -1;
        /// <summary>换边符号（每次交叉后翻转）</summary>
        private float swapSign = 1f;
        private Vector2 lockHead;
        private Vector2 lockLeader;
        private bool locked;
        /// <summary>领节转向的蛇形相位（SteerMovement 持久量）</summary>
        private float leaderSlither;

        public override void OnEnter(FssStateContext ctx) {
            base.OnEnter(ctx);
            phase = Phase.Tear;
            phaseTimer = 0;
            locked = false;
            swapSign = 1f;
            leaderSlither = 0f;
            ctx.RefreshSegments();

            //缝位：链中段就近取囊肿链序（从脓疮处撕开），后半至少留 3 节
            int mid = Math.Max(ctx.TotalSegments / 2, 2);
            seamOrdinal = mid - (mid - 2) % FssDirector.CystStep;
            seamOrdinal = (int)MathHelper.Clamp(seamOrdinal, 2, Math.Max(ctx.TotalSegments - 3, 2));
        }

        public override void OnExit(FssStateContext ctx) {
            //硬保证：任何退出路径（含死亡/超时打断）都清标记 = 缝节恢复跟链
            ctx.SplitLeaderOrdinal = -1;
        }

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;
            NPC leader = FindLeader(ctx);

            //领节失效（被清场/异常）：立即焊合收招
            if (phase != Phase.Tear && leader == null) {
                ctx.SplitLeaderOrdinal = -1;
                return EndAttack(ctx);
            }

            switch (phase) {
                case Phase.Tear:
                    UpdateTear(ctx, npc, leader);
                    break;
                case Phase.Regroup:
                    UpdateRegroup(ctx, npc, leader);
                    break;
                case Phase.Windup:
                    UpdateWindup(ctx, npc, leader);
                    break;
                case Phase.CrossDash:
                    UpdateCrossDash(ctx, npc, leader);
                    break;
                case Phase.Brake:
                    ctx.Mode = FssMoveMode.Direct;
                    npc.velocity *= 0.7f;
                    leader.velocity *= 0.7f;
                    if (phaseTimer >= FssDirector.SunderBrakeFrames) {
                        Counter++;
                        swapSign = -swapSign;
                        phase = Counter >= FssDirector.SunderCrossReps ? Phase.Merge : Phase.Regroup;
                        phaseTimer = 0;
                    }
                    break;
                case Phase.Merge: {
                    IFssState next = UpdateMerge(ctx, npc, leader);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
            }

            phaseTimer++;
            Timer++;

            //超时保险：强制焊合收招（OnExit 兜底清标记）
            if (Timer > 600) {
                ctx.SplitLeaderOrdinal = -1;
                npc.velocity *= 0.8f;
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>撕裂：立身怒吼，缝节渐亮到炸开，后半获得离体初速</summary>
        private void UpdateTear(FssStateContext ctx, NPC npc, NPC leader) {
            ctx.Mode = FssMoveMode.Crawl;
            ctx.CrawlSpeed = 0f;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.LegCommand = FssLegCommand.Raise;
            float w = phaseTimer / (float)FssDirector.SunderTearFrames;
            ctx.FrontRaise = MathHelper.Clamp(w * 1.1f, 0f, 1f);
            ctx.CystGlow = Math.Max(ctx.CystGlow, w);
            ctx.ShakeStrength = Math.Max(ctx.ShakeStrength, w * 0.7f);
            ctx.Compression = Math.Min(ctx.Compression, 1f - 0.1f * w);

            if (phaseTimer == FssDirector.SunderTearFrames - 6 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.75f, Pitch = -0.4f, MaxInstances = 3 }, npc.Center);
            }

            if (phaseTimer >= FssDirector.SunderTearFrames) {
                //撕裂帧：立契 + 断口炸开 + 后半离体
                ctx.SplitLeaderOrdinal = seamOrdinal;
                ctx.PulseWhip(12f);
                if (leader != null) {
                    Vector2 away = (leader.Center - ctx.Target.Center).SafeNormalize(Vector2.UnitX);
                    leader.velocity = away * 8f - new Vector2(0f, 4f);
                    if (!Main.dedServ) {
                        FssVfx.IchorBurst(leader.Center, 2f);
                        FssVfx.CorruptSandBurst(leader.Center, 1f);
                    }
                }
                if (!Main.dedServ) {
                    FssVfx.Roar(npc.Center, -0.45f, 1.2f);
                    FssVfx.Shake(npc.Center, 7f, 1600f);
                    SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 1f, Pitch = -0.35f, MaxInstances = 3 }, npc.Center);
                }
                phase = Phase.Regroup;
                phaseTimer = 0;
            }
        }

        /// <summary>分赴对角锚点：两半各占玩家一侧上方（锚点相隔约 90°）</summary>
        private void UpdateRegroup(FssStateContext ctx, NPC npc, NPC leader) {
            (Vector2 headAnchor, Vector2 leaderAnchor) = Anchors(ctx);

            ctx.Mode = FssMoveMode.Steer;
            ctx.MoveTarget = headAnchor;
            ctx.MoveSpeed = 26f;
            ctx.TurnSpeed = 3f;
            ctx.AccelRate = 0.12f;
            ctx.Slither = 0.4f;
            ctx.LegCommand = FssLegCommand.Flail;

            FssHead.SteerMovement(leader, leaderAnchor, 26f, 2.8f, 0.12f, 0.4f, ref leaderSlither);

            bool headOk = Vector2.Distance(npc.Center, headAnchor) < 130f;
            bool leaderOk = Vector2.Distance(leader.Center, leaderAnchor) < 130f;
            if ((headOk && leaderOk) || phaseTimer > FssDirector.SunderRegroupFrames) {
                phase = Phase.Windup;
                phaseTimer = 0;
                locked = false;
            }
        }

        /// <summary>同帧蓄力：双向后撤 + 双预亮 + 共享提示音，末段各自锁向</summary>
        private void UpdateWindup(FssStateContext ctx, NPC npc, NPC leader) {
            float w = phaseTimer / (float)FssDirector.SunderWindupFrames;
            ctx.CystGlow = Math.Max(ctx.CystGlow, 0.5f + 0.5f * w);
            ctx.LegCommand = FssLegCommand.Tuck;

            if (!locked) {
                Vector2 predict = PredictTarget(ctx, 9f);
                lockHead = (predict - npc.Center).SafeNormalize(Vector2.UnitX);
                lockLeader = (predict - leader.Center).SafeNormalize(Vector2.UnitX);
                if (phaseTimer >= FssDirector.SunderWindupFrames - FssDirector.SunderLockLead) {
                    locked = true;
                    if (!Main.dedServ) {
                        //共享提示音：一声令下双蛇齐动的听觉锚
                        SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.75f, Pitch = 0.6f, MaxInstances = 3 }, ctx.Target.Center);
                    }
                }
            }

            //双向后撤（反向运动即预告）；领节面向冲线不面向撤向
            ctx.Mode = FssMoveMode.Direct;
            npc.velocity = -lockHead * (w * w * 8f);
            npc.rotation = npc.rotation.AngleLerp(lockHead.ToRotation() + FssHead.FacingRot, 0.35f);
            leader.velocity = -lockLeader * (w * w * 8f);
            ctx.SplitLeaderAim = lockLeader.ToRotation();

            if (phaseTimer >= FssDirector.SunderWindupFrames) {
                //同帧交叉出手：一帧定双初速
                npc.velocity = lockHead * FssDirector.SunderDashSpeed * ctx.RampSpeedScale;
                leader.velocity = lockLeader * FssDirector.SunderDashSpeed * ctx.RampSpeedScale;
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                    leader.netUpdate = true;
                }
                ctx.PulseWhip(11f);
                if (!Main.dedServ) {
                    FssVfx.Roar(npc.Center, -0.7f, 0.85f);
                    FssVfx.Shake(ctx.Target.Center, 5f, 1500f);
                }
                phase = Phase.CrossDash;
                phaseTimer = 0;
            }
        }

        /// <summary>交叉冲刺：两线在玩家位交汇成 X，伤害窗速度门控</summary>
        private void UpdateCrossDash(FssStateContext ctx, NPC npc, NPC leader) {
            ctx.Mode = FssMoveMode.Direct;
            ctx.LegCommand = FssLegCommand.Tuck;
            if (npc.velocity.Length() > FssDirector.SkimContactSpeed) {
                npc.damage = npc.defDamage;
            }
            //领节伤害窗由其速度门自理（UpdateSplitLeader）

            if (phaseTimer >= FssDirector.SunderFlightFrames) {
                phase = Phase.Brake;
                phaseTimer = 0;
            }
        }

        /// <summary>合体：领节贴回前半尾节，焊合归一</summary>
        private IFssState UpdateMerge(FssStateContext ctx, NPC npc, NPC leader) {
            //前半缓行等待（头慢爬，前半尾节即焊点随之稳定）
            ctx.Mode = FssMoveMode.Crawl;
            ctx.CrawlSpeed = 5f;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.LegCommand = FssLegCommand.March;

            NPC weldSeg = seamOrdinal - 1 < ctx.Segments.Count && seamOrdinal >= 1
                ? ctx.Segments[seamOrdinal - 1] : npc;
            Vector2 weldPoint = weldSeg.Center;
            FssHead.SteerMovement(leader, weldPoint, 30f, 3.4f, 0.14f, 0.2f, ref leaderSlither);

            if (Vector2.Distance(leader.Center, weldPoint) < FssDirector.SunderMergeSnapDist
                || phaseTimer > FssDirector.SunderMergeFrames) {
                //焊合：清标记恢复跟链 + 金爆 + 鞭波
                ctx.SplitLeaderOrdinal = -1;
                ctx.PulseWhip(12f);
                if (!Main.dedServ) {
                    FssVfx.IchorBurst(leader.Center, 1.6f);
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.9f, Pitch = -0.5f, MaxInstances = 3 }, leader.Center);
                    SoundEngine.PlaySound(SoundID.Item56 with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 3 }, leader.Center);
                    FssVfx.Shake(leader.Center, 4f, 1200f);
                }
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>对角锚点对：玩家上方左右各偏 SunderAnchorSpread，swapSign 换边</summary>
        private (Vector2 head, Vector2 leader) Anchors(FssStateContext ctx) {
            Vector2 center = ctx.Target.Center;
            float headAng = -MathHelper.PiOver2 - FssDirector.SunderAnchorSpread * swapSign;
            float leaderAng = -MathHelper.PiOver2 + FssDirector.SunderAnchorSpread * swapSign;
            return (center + headAng.ToRotationVector2() * FssDirector.SunderAnchorDist,
                center + leaderAng.ToRotationVector2() * FssDirector.SunderAnchorDist);
        }

        /// <summary>按缝链序找领节（列表齐整时下标即链序，另带校验）</summary>
        private NPC FindLeader(FssStateContext ctx) {
            if (seamOrdinal >= 0 && seamOrdinal < ctx.Segments.Count) {
                NPC cand = ctx.Segments[seamOrdinal];
                if (cand.Alives() && (int)cand.ai[0] == seamOrdinal) {
                    return cand;
                }
            }
            foreach (var seg in ctx.Segments) {
                if (seg.Alives() && (int)seg.ai[0] == seamOrdinal) {
                    return seg;
                }
            }
            return null;
        }
    }
}
