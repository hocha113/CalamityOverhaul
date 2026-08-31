using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.States
{
    /// <summary>
    /// 长镰自刈：半立起 → 长镰沿自体侧翼逐颗割开自己的囊肿（预亮 → 切弧中点割破 →
    /// 定向灵液喷扇，落点留池；消耗 CystSpent 充能，与疮爆掠航共享资源口径）→
    /// 镰尖过顶回甩一梳重团痰弹收势。变异身份的自残式攻击。
    /// 公平口径：每颗割前有切弧前摇 + 囊肿预亮；喷扇慢弧可读；蛇半锚定 =
    /// 输出白给窗；资源不足时跳过割取只做甩痰短版（hub 替补由充能自然调度）。
    /// 头全程看着自己下刀（自残读数的点睛）。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.ClawReap, typeof(FssStateContext))]
    internal class FssClawReapState : FssStateBase
    {
        public override string StateName => "ClawReap";
        public override FssStateIndex StateIndex => FssStateIndex.ClawReap;

        private enum ReapPhase
        {
            Raise, //半立起蓄势
            Slice, //逐颗割取
            Fling, //镰尖甩痰收势
        }

        private ReapPhase phase;
        /// <summary>割取目标链序表（进场按确定性规则选定，各端一致）</summary>
        private readonly List<int> cuts = new();
        /// <summary>当前割到第几颗</summary>
        private int cutIndex;

        public override void OnEnter(FssStateContext ctx) {
            base.OnEnter(ctx);
            phase = ReapPhase.Raise;
            cutIndex = 0;
            cuts.Clear();

            //确定性选目标：链序升序取未瘪的露地囊肿（各端同规则同结果）
            int want = FssDirector.ReapCuts(ctx.Phase);
            foreach (var seg in ctx.Segments) {
                if (cuts.Count >= want) {
                    break;
                }
                int ordinal = (int)seg.ai[0];
                if (!seg.Alives() || !FssStateContext.IsCystOrdinal(ordinal)
                    || ordinal >= ctx.CystSpent.Length
                    || ctx.CystSpent[ordinal] > FssDirector.ReapCystThreshold) {
                    continue;
                }
                cuts.Add(ordinal);
            }
        }

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case ReapPhase.Raise:
                    UpdateRaise(ctx, npc);
                    break;
                case ReapPhase.Slice:
                    UpdateSlice(ctx, npc);
                    break;
                case ReapPhase.Fling: {
                    IFssState next = UpdateFling(ctx, npc);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
            }

            //超时保险兜底
            if (Counter++ > 60 * 7) {
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(ReapPhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>半立起：慢刹立身，长镰高举（立起剪影 + 镰光即预告）</summary>
        private void UpdateRaise(FssStateContext ctx, NPC npc) {
            ctx.Mode = FssMoveMode.Direct;
            npc.velocity *= 0.86f;
            ctx.LegCommand = FssLegCommand.Raise;
            ctx.FrontRaise = MathHelper.Clamp(Timer / (float)FssDirector.ReapRaiseFrames, 0f, 0.7f);
            ctx.Compression = Math.Min(ctx.Compression, 0.94f);
            ctx.ClawCommand = FssClawCommand.Fling;
            ctx.ClawPhase = 0.15f;

            if ((int)Timer == 2 && !Main.dedServ) {
                FssVfx.Roar(npc.Center, -0.45f, 0.85f);
            }

            Timer++;
            if (Timer >= FssDirector.ReapRaiseFrames) {
                SwitchPhase(cuts.Count > 0 ? ReapPhase.Slice : ReapPhase.Fling);
            }
        }

        /// <summary>
        /// 逐颗割取：镰尖追着囊肿节走切弧，弧中点割破——瘪缩（CystSpent 置满）+
        /// 定向喷扇（留池模式）；头看着自己下刀。
        /// </summary>
        private void UpdateSlice(FssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            int local = t % FssDirector.ReapSliceFrames;

            //半锚定慢爬（保持切割姿态）
            ctx.Mode = FssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = 3f;
            ctx.LegCommand = FssLegCommand.March;
            ctx.FrontRaise = 0.45f;

            NPC seg = ResolveCut(ctx, cutIndex);
            if (seg == null) {
                //目标失效（体节没了/已瘪）：跳下一颗
                cutIndex++;
                if (cutIndex >= cuts.Count) {
                    SwitchPhase(ReapPhase.Fling);
                }
                Timer = 0;
                return;
            }

            //镰走切弧，头看着下刀处（自残读数）
            ctx.ClawCommand = FssClawCommand.Reap;
            ctx.ClawPhase = local / (float)FssDirector.ReapSliceFrames;
            ctx.ClawAim = seg.Center;
            ctx.AimAngle = (seg.Center - npc.Center).ToRotation();

            //割前预亮：本颗囊肿升辉
            if (local < FssDirector.ReapSliceFrames / 2) {
                ctx.CystGlow = Math.Max(ctx.CystGlow, 0.85f);
            }

            //切弧中点：割破
            if (local == FssDirector.ReapSliceFrames / 2) {
                int ordinal = (int)seg.ai[0];
                if (ordinal < ctx.CystSpent.Length) {
                    ctx.CystSpent[ordinal] = 1f; //瘪缩 + 熄灯（与疮爆掠航共享资源口径）
                }
                ctx.PulseWhip(6f);
                if (!Main.dedServ) {
                    FssVfx.IchorBurst(seg.Center, 1.5f,
                        ctx.Target.Alives()
                            ? (ctx.Target.Center - seg.Center).SafeNormalize(-Vector2.UnitY)
                            : -Vector2.UnitY);
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.75f, Pitch = -0.2f, MaxInstances = 4 }, seg.Center);
                    FssVfx.Shake(seg.Center, 3f, 1000f);
                }
                //定向喷扇（权威端；留池模式，落点播种）
                if (!VaultUtils.isClient && ctx.Target.Alives()) {
                    int damage = FssDirector.ScaleProjectileDamage(npc, FssDirector.IchorGlobDamage);
                    int type = ModContent.ProjectileType<FssIchorGlob>();
                    Vector2 aim = (PredictTarget(ctx, 14f) - seg.Center).SafeNormalize(Vector2.UnitX);
                    for (int i = 0; i < FssDirector.ReapFanDrops; i++) {
                        float spread = MathHelper.Lerp(-0.5f, 0.5f,
                            FssDirector.ReapFanDrops > 1 ? i / (float)(FssDirector.ReapFanDrops - 1) : 0.5f);
                        Vector2 vel = aim.RotatedBy(spread)
                            * FssDirector.ReapFanSpeed * ctx.RampSpeedScale
                            * Main.rand.NextFloat(0.9f, 1.1f)
                            - new Vector2(0f, 1.6f);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), seg.Center, vel,
                            type, damage, 0.5f, Main.myPlayer);
                    }
                }
            }

            Timer++;
            if (local == FssDirector.ReapSliceFrames - 1) {
                cutIndex++;
                if (cutIndex >= cuts.Count) {
                    SwitchPhase(ReapPhase.Fling);
                }
            }
        }

        /// <summary>解析当前割取目标（链序 → 活体节）</summary>
        private NPC ResolveCut(FssStateContext ctx, int index) {
            if (index >= cuts.Count) {
                return null;
            }
            int ordinal = cuts[index];
            foreach (var seg in ctx.Segments) {
                if (seg.Alives() && (int)seg.ai[0] == ordinal) {
                    return seg;
                }
            }
            return null;
        }

        /// <summary>镰尖过顶回甩：慢引快甩，甩出帧从镰尖（编舞同源点）抛重团痰弹</summary>
        private IFssState UpdateFling(FssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            float fling01 = MathHelper.Clamp(t / (float)FssDirector.ReapFlingFrames, 0f, 1f);

            ctx.Mode = FssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = 4f;
            ctx.FrontRaise = 0.5f * (1f - fling01);
            ctx.ClawCommand = FssClawCommand.Fling;
            ctx.ClawPhase = fling01;
            if (ctx.Target.Alives()) {
                ctx.AimAngle = (ctx.Target.Center - npc.Center).ToRotation();
            }

            //甩出帧：镰尖即弹幕出生点（与绘制同一编舞函数；镰对锚在尾前节）
            if (t == (int)(FssDirector.ReapFlingFrames * 0.85f)) {
                Vector2 rearCenter = npc.Center;
                float rearRot = npc.rotation;
                int rearIndex = ctx.Segments.Count - 4;
                if (rearIndex >= 2 && rearIndex < ctx.Segments.Count
                    && ctx.Segments[rearIndex] is { active: true } rearSeg) {
                    rearCenter = rearSeg.Center;
                    rearRot = rearSeg.rotation;
                }
                Vector2 tip = FssClawScript.FlingTip(rearCenter, rearRot, 1, 0.85f, npc.scale);
                ctx.PulseGapWave(SerpentChainMath.WaveRelease, 0.08f);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.65f, Pitch = -0.1f, MaxInstances = 3 }, tip);
                    FssVfx.IchorBurst(tip, 1f);
                }
                if (!VaultUtils.isClient && ctx.Target.Alives()) {
                    int damage = FssDirector.ScaleProjectileDamage(npc, FssDirector.IchorGlobDamage);
                    int type = ModContent.ProjectileType<FssIchorGlob>();
                    Vector2 aim = (PredictTarget(ctx, 16f) - tip).SafeNormalize(Vector2.UnitX);
                    for (int i = 0; i < FssDirector.ReapFlingGlobs; i++) {
                        float spread = MathHelper.Lerp(-0.22f, 0.22f,
                            FssDirector.ReapFlingGlobs > 1 ? i / (float)(FssDirector.ReapFlingGlobs - 1) : 0.5f);
                        Vector2 vel = aim.RotatedBy(spread)
                            * (FssDirector.IchorGlobSpeed + 2f) * ctx.RampSpeedScale
                            - new Vector2(0f, 2.4f);
                        //mode 2 重团：命中/落地都砸大池
                        Projectile.NewProjectile(npc.GetSource_FromAI(), tip, vel,
                            type, damage, 0.7f, Main.myPlayer, 2f);
                    }
                }
            }

            Timer++;
            if (t > FssDirector.ReapFlingFrames + 8) {
                return EndAttack(ctx);
            }
            return null;
        }
    }
}
