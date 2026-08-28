using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.States
{
    /// <summary>
    /// A6 灵液瀑洗（P2 起）：爬升到玩家侧上锚点 S 形悬游，持续呕吐灵液水管
    /// 以固定角速横扫场地（高拉伸痰滴链首尾相接读成连续瀑流），稀疏播种脓池。
    /// 公平口径：扫向走时间表不追踪玩家（扫域绕悬点正下方对称）、
    /// 折返必断流 HosePauseFrames = 声明逃生拍、锚点只慢速跟随（瀑流几何稳定可读）。
    /// P3 三趟扫。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.CascadeHose, typeof(FssStateContext))]
    internal class FssCascadeHoseState : FssStateBase
    {
        public override string StateName => "CascadeHose";
        public override FssStateIndex StateIndex => FssStateIndex.CascadeHose;

        private enum Phase { Climb, Sweep, Pause, Exit }

        private Phase phase;
        private int phaseTimer;
        /// <summary>已完成的单向扫数</summary>
        private int sweepsDone;
        /// <summary>悬游锚点（慢速跟随玩家）</summary>
        private Vector2 anchor;
        /// <summary>本趟扫的起始符号（正=从右往左）</summary>
        private float sweepSign = 1f;
        private int dropIndex;

        public override void OnEnter(FssStateContext ctx) {
            base.OnEnter(ctx);
            phase = Phase.Climb;
            phaseTimer = 0;
            sweepsDone = 0;
            dropIndex = 0;
            float side = Math.Sign(ctx.Npc.Center.X - ctx.Target.Center.X);
            if (side == 0f) {
                side = 1f;
            }
            anchor = HoverPoint(ctx, side);
            sweepSign = side;
        }

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;

            //锚点慢速跟随（0.02 = 瀑流几何基本稳定，玩家跑不出屏也甩不掉扫域）
            float anchorSide = Math.Sign(anchor.X - ctx.Target.Center.X);
            if (anchorSide == 0f) {
                anchorSide = 1f;
            }
            anchor = Vector2.Lerp(anchor, HoverPoint(ctx, anchorSide), 0.02f);

            //悬游：S 形绕锚点游动（腾空划桨腿）
            ctx.Mode = FssMoveMode.Steer;
            ctx.MoveTarget = anchor + new Vector2(
                MathF.Sin(Timer * 0.045f) * 90f,
                MathF.Sin(Timer * 0.07f) * 46f);
            ctx.MoveSpeed = 11f;
            ctx.TurnSpeed = 3f;
            ctx.AccelRate = 0.1f;
            ctx.Slither = 0.85f;
            ctx.LegCommand = FssLegCommand.Flail;

            switch (phase) {
                case Phase.Climb:
                    //爬升段全速赶位（悬游慢速只在扫射期）
                    ctx.MoveSpeed = 25f;
                    ctx.AccelRate = 0.13f;
                    if (Vector2.Distance(npc.Center, anchor) < 110f || phaseTimer > FssDirector.HoseClimbFrames) {
                        phase = Phase.Sweep;
                        phaseTimer = 0;
                        HoseCue(npc, 0.5f);
                    }
                    break;

                case Phase.Sweep:
                    UpdateSweep(ctx, npc);
                    if (phaseTimer >= FssDirector.HoseSweepFrames) {
                        sweepsDone++;
                        sweepSign = -sweepSign;
                        phase = Phase.Pause;
                        phaseTimer = 0;
                        //断流吸气（声明逃生拍的听觉边界）
                        if (!Main.dedServ) {
                            SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.6f, Pitch = -0.55f, MaxInstances = 3 }, npc.Center);
                        }
                    }
                    break;

                case Phase.Pause:
                    if (phaseTimer >= FssDirector.HosePauseFrames) {
                        if (sweepsDone >= FssDirector.HoseSweeps(ctx.Phase)) {
                            phase = Phase.Exit;
                            phaseTimer = 0;
                            break;
                        }
                        phase = Phase.Sweep;
                        phaseTimer = 0;
                        HoseCue(npc, 0.5f);
                    }
                    break;

                case Phase.Exit: {
                    //俯冲回地：高空直接回 hub 会让下一记贴地招在半空开冲
                    ctx.Mode = FssMoveMode.Direct;
                    ctx.LegCommand = FssLegCommand.Flail;
                    npc.velocity.X *= 0.97f;
                    npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y + 0.75f, -8f, 22f);
                    float groundY = FssVfx.FindGroundY(npc.Center - new Vector2(0f, 60f));
                    if (npc.Center.Y >= groundY - FssDirector.CrawlRideHeight - 30f || phaseTimer > 46) {
                        npc.velocity.Y *= 0.3f;
                        if (!Main.dedServ && phaseTimer <= 46) {
                            FssVfx.CorruptSandBurst(new Vector2(npc.Center.X, groundY), 1f);
                        }
                        return EndAttack(ctx);
                    }
                    break;
                }
            }

            phaseTimer++;
            Timer++;

            //超时保险
            int budget = FssDirector.HoseClimbFrames
                + FssDirector.HoseSweeps(3) * (FssDirector.HoseSweepFrames + FssDirector.HosePauseFrames) + 110;
            if (Timer > budget) {
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>单趟横扫：固定角速时间表，痰滴链成瀑流</summary>
        private void UpdateSweep(FssStateContext ctx, NPC npc) {
            float tt = phaseTimer / (float)FssDirector.HoseSweepFrames;
            //扫域绕正下方对称：sweepSign 决定本趟从哪一侧扫起
            float angle = MathHelper.PiOver2
                + MathHelper.Lerp(FssDirector.HoseArcHalf * sweepSign, -FssDirector.HoseArcHalf * sweepSign, tt);
            Vector2 aim = angle.ToRotationVector2();
            ctx.AimAngle = angle;
            ctx.CystGlow = Math.Max(ctx.CystGlow, 0.85f);
            ctx.SwallowSuction = Math.Max(ctx.SwallowSuction, 0.4f);
            //持续喷射的反推：悬游目标点被水管推力顶离喷向（质量即反作用）
            ctx.MoveTarget -= aim * 30f;

            Vector2 mouth = MouthPos(npc);

            //口沫与逐口后坐（表现层）
            if (!Main.dedServ) {
                if (phaseTimer % 9 == 0) {
                    SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.5f, Pitch = -0.35f, MaxInstances = 5 }, mouth);
                }
                if (Main.rand.NextBool(2)) {
                    FssVfx.IchorBurst(mouth, 0.35f, aim);
                }
            }
            if (phaseTimer % 12 == 0) {
                ctx.PulseWhip(3.5f);
            }

            //痰滴链（权威端）：每 HoseDropGap 一滴，每 HosePoolEvery 滴一颗留池
            if (!VaultUtils.isClient && phaseTimer % FssDirector.HoseDropGap == 0) {
                dropIndex++;
                int damage = FssDirector.ScaleProjectileDamage(npc, FssDirector.CascadeDamage);
                float poolFlag = dropIndex % FssDirector.HosePoolEvery == 0 ? 1f : 0f;
                Vector2 vel = aim * FssDirector.HoseDropSpeed * ctx.RampSpeedScale
                    + npc.velocity * 0.25f;
                Projectile.NewProjectile(npc.GetSource_FromAI(), mouth, vel,
                    ModContent.ProjectileType<FssCascadeDrop>(), damage, 0.4f, Main.myPlayer, poolFlag);
            }
        }

        /// <summary>起扫提示音</summary>
        private static void HoseCue(NPC npc, float volume) {
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = volume, Pitch = 0.4f, MaxInstances = 3 }, npc.Center);
            }
        }

        /// <summary>悬游锚点：玩家地表上方 HoseAltitude、侧偏 HoseSideOffset</summary>
        private static Vector2 HoverPoint(FssStateContext ctx, float side) {
            float groundY = FssVfx.FindGroundY(ctx.Target.Center - new Vector2(0f, 200f));
            float topY = Math.Min(groundY, ctx.Target.Center.Y);
            return new Vector2(ctx.Target.Center.X + side * FssDirector.HoseSideOffset,
                topY - FssDirector.HoseAltitude);
        }
    }
}
