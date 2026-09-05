using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 鳌足扬沙：面向玩家站定 → 双螯插进身前沙面掘沙（螯尖渗沙 = 预告）→ 过顶抡起拉弓 →
    /// 鞭向前上方甩出一梳高弧沙团 → 沙雨从头顶落下，落点包夹玩家预测位。
    /// 鳌足从表演件变成武器的第一招，也是贴地身份下的对空答案（弧顶约 400px）。
    /// 公平阀声明：低重力高弧滞空约 2 秒、全程可见可预读；落点间距 RainSpacing 即站缝；
    /// 出手帧锁定预测位后不再追瞄；沙团不追踪。P2 起两掷（第二掷重新掘沙 = 重新预告）。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.ClawFling, typeof(BssStateContext))]
    internal class BssClawFlingState : BssStateBase
    {
        public override string StateName => "ClawFling";
        public override BssStateIndex StateIndex => BssStateIndex.ClawFling;

        private enum FlingPhase
        {
            Approach, //贴到出手距离、面向玩家
            Scoop,    //双螯掘沙
            Hurl,     //过顶抡掷
            Recover,  //收势
        }

        private FlingPhase phase;
        private int reps;
        private float toward = 1f;
        private bool released;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phase = FlingPhase.Approach;
            reps = 0;
            released = false;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case FlingPhase.Approach:
                    UpdateApproach(ctx, npc);
                    break;
                case FlingPhase.Scoop:
                    UpdateScoop(ctx, npc);
                    break;
                case FlingPhase.Hurl:
                    UpdateHurl(ctx, npc);
                    break;
                case FlingPhase.Recover: {
                    IBssState next = UpdateRecover(ctx, npc);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
            }

            //超时保险兜底
            if (Counter++ > 60 * 6) {
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(FlingPhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>就位：进距离带并面向玩家即早退</summary>
        private void UpdateApproach(BssStateContext ctx, NPC npc) {
            float dx = ctx.Target.Center.X - npc.Center.X;
            toward = FacingToTarget(ctx, 0f);

            ctx.Mode = BssMoveMode.Crawl;
            ctx.LegCommand = BssLegCommand.March;
            bool inRange = Math.Abs(dx) <= BssDirector.FlingRange;
            ctx.CrawlDirX = toward;
            ctx.CrawlSpeed = inRange ? BssDirector.CrawlTurnSpeed : BssDirector.CrawlChaseSpeed;

            Timer++;
            bool facing = Math.Sign(npc.velocity.X) == Math.Sign(toward) || Math.Abs(npc.velocity.X) < 2.5f;
            if ((inRange && facing && Timer >= 6) || Timer >= BssDirector.FlingApproachFrames) {
                toward = FacingToTarget(ctx, 0f);
                if (!Main.dedServ) {
                    BssVfx.Roar(npc.Center, 0f, 0.5f);
                }
                SwitchPhase(FlingPhase.Scoop);
            }
        }

        /// <summary>掘沙：站定压低，双螯插沙舀住（螯尖渗沙 + 挖沙声），头微抬看向目标</summary>
        private void UpdateScoop(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            float progress = MathHelper.Clamp(t / (float)BssDirector.FlingScoopFrames, 0f, 1f);

            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlSpeed = 0f;
            ctx.CrawlDirX = toward;
            ctx.LegCommand = BssLegCommand.Brace;
            ctx.ClawCommand = BssClawCommand.Scoop;
            ctx.ClawPhase = progress;
            ctx.FrontRaise = 0.3f * progress;
            ctx.Compression = MathHelper.Lerp(1f, 0.92f, progress);
            ctx.AimAngle = new Vector2(toward, -0.35f).ToRotation();
            DeclareJaw(ctx, BssJawCommand.Inhale, progress * 0.6f);

            if (!Main.dedServ) {
                //螯尖插沙处渗沙：与 Scoop 编舞的落点同源（头前下方两侧）
                float groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 60f), 400f);
                for (int side = -1; side <= 1; side += 2) {
                    if (!Main.rand.NextBool(2)) {
                        continue;
                    }
                    Vector2 dig = new(npc.Center.X + toward * (70f + side * 12f) * npc.scale + Main.rand.NextFloat(-10f, 10f), groundY - 4f);
                    Dust d = Dust.NewDustPerfect(dig, DustID.Sand,
                        new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 3f) * (0.5f + progress)),
                        100, default, Main.rand.NextFloat(0.9f, 1.3f) * (0.7f + 0.5f * progress));
                    d.noGravity = false;
                }
                if (t % 7 == 0) {
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = -0.3f + 0.3f * progress, MaxInstances = 3 }, npc.Center);
                }
            }

            Timer++;
            if (t >= BssDirector.FlingScoopFrames) {
                released = false;
                SwitchPhase(FlingPhase.Hurl);
            }
        }

        /// <summary>抡掷：双螯抡过头顶再鞭向前上，释放帧甩出一梳高弧沙团</summary>
        private void UpdateHurl(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            float progress = MathHelper.Clamp(t / (float)BssDirector.FlingHurlFrames, 0f, 1f);

            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlSpeed = 0f;
            ctx.CrawlDirX = toward;
            ctx.LegCommand = BssLegCommand.Brace;
            ctx.ClawCommand = BssClawCommand.Fling;
            ctx.ClawPhase = progress;
            //抡起段头后仰蓄势（聚拢上膛），鞭出段头前探张口
            float whipStart = BssClawScript.FlingWhipStart;
            bool windingUp = progress < whipStart;
            float windup = MathHelper.Clamp(progress / whipStart, 0f, 1f);
            float whip = MathHelper.Clamp((progress - whipStart) / (1f - whipStart), 0f, 1f);
            float lean = windingUp ? -0.55f * windup : -0.55f + 1.1f * whip;
            ctx.FrontRaise = 0.3f + 0.35f * windup;
            ctx.AimAngle = new Vector2(toward, -0.35f + lean * 0.4f).ToRotation();
            ctx.GatherLevel = windingUp ? windup : 0f;
            DeclareJaw(ctx, windingUp ? BssJawCommand.Inhale : BssJawCommand.Gape, 0.6f);

            if (!released && t >= BssDirector.FlingReleaseFrame) {
                released = true;
                Release(ctx, npc);
            }

            Timer++;
            if (t >= BssDirector.FlingHurlFrames) {
                reps++;
                SwitchPhase(FlingPhase.Recover);
            }
        }

        /// <summary>
        /// 出手：一梳高弧沙团（低重力变体），落点以预测位为中心按 RainSpacing 包夹，
        /// 固定竖直初速反解横速（弧顶同高 = 齐落成排）。释放反冲 + 鞭波 + 甩沙声。
        /// </summary>
        private void Release(BssStateContext ctx, NPC npc) {
            ctx.PulseWhip(8f);
            ctx.PulseGapWave(SerpentChainMath.WaveRelease, 0.1f);
            ctx.ClawBurst = 1f;
            npc.velocity -= new Vector2(toward * 3f, 0f);
            Vector2 release = npc.Center + new Vector2(toward * 30f, -70f) * npc.scale;

            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = -0.6f, MaxInstances = 2 }, npc.Center);
                BssVfx.Roar(npc.Center, -0.2f, 0.7f);
                for (int i = 0; i < 16; i++) {
                    Dust d = Dust.NewDustPerfect(release + Main.rand.NextVector2Circular(20f, 12f), DustID.Sand,
                        new Vector2(toward * Main.rand.NextFloat(1f, 4f), -Main.rand.NextFloat(4f, 9f)),
                        90, default, Main.rand.NextFloat(1f, 1.5f));
                    d.noGravity = false;
                }
            }

            if (VaultUtils.isClient || !ctx.Target.Alives()) {
                return;
            }
            int count = BssDirector.RainGlobs(ctx.Phase);
            int damage = BssDirector.ScaleProjectileDamage(npc, BssDirector.SandGlobDamage);
            int type = ModContent.ProjectileType<BssSandGlob>();
            float g = BssDirector.SandGlobGravity * BssDirector.RainGlobGravityMul;
            float vy = BssDirector.RainGlobLaunchVy;
            Vector2 predicted = ctx.Target.Center + ctx.Target.velocity * 30f;
            for (int i = 0; i < count; i++) {
                float landX = predicted.X + (i - (count - 1) * 0.5f) * BssDirector.RainSpacing;
                float landY = BssVfx.FindGroundY(new Vector2(landX, Math.Min(predicted.Y, release.Y) - 200f));
                float dy = landY - release.Y;
                //竖直初速固定，解落地时间：0.5g t² + vy t − dy = 0
                float disc = vy * vy + 2f * g * dy;
                float tf = disc > 0f ? (-vy + MathF.Sqrt(disc)) / g : 90f;
                tf = MathHelper.Clamp(tf, 60f, 170f);
                float vx = (landX - release.X) / tf;
                Projectile.NewProjectile(npc.GetSource_FromAI(), release, new Vector2(vx, vy), type, damage, 0.5f,
                    Main.myPlayer, 1f);
            }
            npc.netUpdate = true;
        }

        /// <summary>收势：吼一拍压回去，连掷未满回就位重掘</summary>
        private IBssState UpdateRecover(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = MathHelper.Lerp(0f, BssDirector.CrawlCruiseSpeed,
                MathHelper.Clamp(t / (float)BssDirector.FlingRecoverFrames, 0f, 1f));
            ctx.LegCommand = BssLegCommand.March;
            if (t < 8) {
                ctx.ClawCommand = BssClawCommand.Fling;
                ctx.ClawPhase = 1f;
                DeclareJaw(ctx, BssJawCommand.Gape);
            }

            Timer++;
            if (t >= BssDirector.FlingRecoverFrames) {
                if (reps < BssDirector.FlingReps(ctx.Phase)) {
                    SwitchPhase(FlingPhase.Approach);
                    return null;
                }
                return EndAttack(ctx);
            }
            return null;
        }
    }
}
