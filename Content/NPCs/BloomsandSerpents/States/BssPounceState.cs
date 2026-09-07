using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 蹲伏扑击：面向玩家站定 → 八腿蹲紧、双螯高举大张、头盯落点 →
    /// 一帧蹬地抛物跃扑 → 落地沙爆（P2 起掀沙球扇）→ 收势。
    /// 腿架的高光招：蹲伏（Brace 站距外扩）、腾空（Flail 抓空）、落地（全髋下沉）三拍都是腿在演。
    /// 公平阀：蹲伏姿态本身就是预告（不画预判线），末 PounceLockLead 帧锁向（预告即承诺，出手不再追瞄）；
    /// 抛物弹道可读；伤害窗 = 速度门槛；起跳距离带 [PounceMinRange, PounceMaxRange] 杀贴脸秒杀。
    /// 高飞玩家也够得着（弹道反解朝预测位起跳），是贴地掠冲的对空替补。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.Pounce, typeof(BssStateContext))]
    internal class BssPounceState : BssStateBase
    {
        public override string StateName => "Pounce";
        public override BssStateIndex StateIndex => BssStateIndex.Pounce;

        private enum PouncePhase
        {
            Stalk,   //面向玩家、进距离带
            Crouch,  //蹲伏蓄势 + 预警线
            Flight,  //抛物跃扑
            Recover, //落地收势
        }

        private PouncePhase phase;
        private int pounces;
        /// <summary>锁定的起跳速度（锁向帧后不再更新 = 预告即承诺）</summary>
        private Vector2 launchVel;
        private float toward = 1f;
        private bool landedFx;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phase = PouncePhase.Stalk;
            pounces = 0;
            landedFx = false;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case PouncePhase.Stalk:
                    UpdateStalk(ctx, npc);
                    break;
                case PouncePhase.Crouch:
                    UpdateCrouch(ctx, npc);
                    break;
                case PouncePhase.Flight:
                    UpdateFlight(ctx, npc);
                    break;
                case PouncePhase.Recover: {
                    IBssState next = UpdateRecover(ctx, npc);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
            }

            //超时保险兜底
            if (Counter++ > 60 * 8) {
                npc.velocity *= 0.6f;
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(PouncePhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>就位：进起跳距离带并面向玩家（转身完成才蹲），超时直接蹲</summary>
        private void UpdateStalk(BssStateContext ctx, NPC npc) {
            float dx = ctx.Target.Center.X - npc.Center.X;
            float absDx = Math.Abs(dx);
            toward = FacingToTarget(ctx, 0f);

            ctx.Mode = BssMoveMode.Crawl;
            ctx.LegCommand = BssLegCommand.March;
            bool inBand = absDx >= BssDirector.PounceMinRange && absDx <= BssDirector.PounceMaxRange;
            if (absDx < BssDirector.PounceMinRange) {
                //太近先退开：扑击要有可读的弧线
                ctx.CrawlDirX = -toward;
                ctx.CrawlSpeed = BssDirector.CrawlChaseSpeed;
            }
            else if (absDx > BssDirector.PounceMaxRange) {
                ctx.CrawlDirX = toward;
                ctx.CrawlSpeed = BssDirector.CrawlChaseSpeed;
            }
            else {
                //带内：面向玩家慢下来
                ctx.CrawlDirX = toward;
                ctx.CrawlSpeed = BssDirector.CrawlTurnSpeed;
            }

            Timer++;
            bool facing = Math.Sign(npc.velocity.X) == Math.Sign(toward) || Math.Abs(npc.velocity.X) < 2.5f;
            if ((inBand && facing && Timer >= 8) || Timer >= BssDirector.PounceStalkFrames) {
                toward = FacingToTarget(ctx, 0f);
                SwitchPhase(PouncePhase.Crouch);
            }
        }

        /// <summary>扑击瞄准点：预测半程飞行时间的玩家位</summary>
        private static Vector2 PounceAimPoint(Player target)
            => target.Center + target.velocity * (BssDirector.PounceFlightTime * 0.5f);

        /// <summary>蹲伏蓄势：站定、八腿蹲紧、双螯举张、头追瞄落点；末段锁定弹道</summary>
        private void UpdateCrouch(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            float progress = MathHelper.Clamp(t / (float)BssDirector.PounceCrouchFrames, 0f, 1f);

            //锁向拍之前逐帧反解弹道，之后死向
            if (t <= BssDirector.PounceCrouchFrames - BssDirector.PounceLockLead) {
                launchVel = SolveLaunch(npc.Center, PounceAimPoint(ctx.Target));
            }

            if (t == 0 && !Main.dedServ) {
                BssVfx.Roar(npc.Center, -0.1f, 0.6f);
                SoundEngine.PlaySound(SoundID.Item102 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 2 }, npc.Center);
            }

            //蹲伏：站定压低，身体向头收拢上膛；头看向预测落点
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlSpeed = 0f;
            ctx.CrawlDirX = toward;
            ctx.LegCommand = BssLegCommand.Brace;
            ctx.ClawCommand = BssClawCommand.Brace;
            ctx.ClawPhase = progress;
            ctx.Compression = MathHelper.Lerp(1f, 0.84f, progress);
            ctx.GatherLevel = progress;
            ctx.AimAngle = launchVel.LengthSquared() > 1f
                ? launchVel.ToRotation()
                : (ctx.Target.Center - npc.Center).ToRotation();
            DeclareJaw(ctx, BssJawCommand.Inhale, progress);

            //末段绷紧颤抖（绘制层抖动通道，位置不动）
            if (progress > 0.6f) {
                ctx.ShakeStrength = Math.Max(ctx.ShakeStrength, 0.26f);
            }
            //脚下渗沙（各端本地）
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                BssVfx.SandTrickle(npc.Bottom + new Vector2(Main.rand.NextFloat(-60f, 60f), 0f), 0.6f + progress);
            }

            Timer++;
            if (t >= BssDirector.PounceCrouchFrames) {
                Launch(ctx, npc);
                SwitchPhase(PouncePhase.Flight);
            }
        }

        /// <summary>
        /// 弹道反解：定飞行时间朝落点起跳，速度上限钳制，竖直分量至少上抛一截
        /// （即使玩家在下方，也先起跳再落，扑击的弧线身份）
        /// </summary>
        private static Vector2 SolveLaunch(Vector2 from, Vector2 to) {
            float tf = BssDirector.PounceFlightTime;
            float g = BssDirector.PounceGravity;
            Vector2 d = to - from;
            float vx = d.X / tf;
            float vy = d.Y / tf - 0.5f * g * tf;
            vy = Math.Min(vy, -7f);
            Vector2 v = new(vx, vy);
            float speed = v.Length();
            if (speed > BssDirector.PounceMaxSpeed) {
                v *= BssDirector.PounceMaxSpeed / speed;
            }
            return v;
        }

        /// <summary>蹬地起跳：一帧定初速 + 沙爆 + 吼声 + 鞭链行波</summary>
        private void Launch(BssStateContext ctx, NPC npc) {
            npc.velocity = launchVel;
            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
            }
            landedFx = false;
            ctx.PulseWhip(12f);
            ctx.PulseGapWave(SerpentChainMath.WaveRelease, 0.15f);
            DeclareJaw(ctx, BssJawCommand.Gape);
            if (!Main.dedServ) {
                BssVfx.SandBurst(npc.Bottom, 1.5f);
                BssVfx.Roar(npc.Center, -0.4f, 1f);
                BssVfx.Shake(npc.Center, 6f, 1200f);
            }
        }

        /// <summary>腾空：抛物直线承诺不追瞄，速度门槛开伤害窗，贴近地面即落地</summary>
        private void UpdateFlight(BssStateContext ctx, NPC npc) {
            ctx.Mode = BssMoveMode.Direct;
            ctx.LegCommand = BssLegCommand.Flail;
            npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y + BssDirector.PounceGravity, -32f, 24f);
            DeclareJaw(ctx, BssJawCommand.Gape);

            float speed = npc.velocity.Length();
            if (speed > BssDirector.PounceContactSpeed) {
                npc.damage = npc.defDamage;
                DeclareSnatchIfClose(ctx, npc, BssDirector.PounceContactSpeed);
            }

            Timer++;
            //落地判定：下坠中贴到地表（起跳后至少 8 帧，防在起跳点原地触地）
            bool landed = false;
            if (Timer > 8 && npc.velocity.Y > 0f) {
                float groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 200f), 700f);
                landed = npc.Center.Y >= groundY - BssDirector.CrawlRideHeight;
            }
            if (landed || Timer > 110) {
                Land(ctx, npc);
                SwitchPhase(PouncePhase.Recover);
            }
        }

        /// <summary>落地：重震 + 全髋下沉 + 沙爆 + 追压波；P2 起落点掀沙球扇（两侧贴地留逃生道）</summary>
        private void Land(BssStateContext ctx, NPC npc) {
            pounces++;
            npc.velocity = new Vector2(npc.velocity.X * 0.15f, 0f);
            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
            }
            ctx.PulseWhip(10f);
            ctx.PulseGapWave(SerpentChainMath.WavePress, 0.14f);
            for (int k = 0; k < ctx.StationBob.Length; k++) {
                ctx.StationBob[k] = 1.25f;
            }
            float groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 200f), 700f);
            Vector2 ground = new(npc.Center.X, groundY);
            if (!landedFx) {
                landedFx = true;
                if (!Main.dedServ) {
                    BssVfx.SandBurst(ground, 1.9f);
                    BssVfx.Roar(npc.Center, -0.5f, 0.9f);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = -0.6f, MaxInstances = 2 }, ground);
                    BssVfx.Shake(npc.Center, 8f, 1400f);
                }
                if (ctx.Phase >= 2) {
                    BssVfx.BreachEruption(npc, ground, BssDirector.PounceLandGlobs);
                }
            }
        }

        /// <summary>收势：吼一拍再起身，连扑未满回就位</summary>
        private IBssState UpdateRecover(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = MathHelper.Lerp(0f, BssDirector.CrawlCruiseSpeed,
                MathHelper.Clamp(t / (float)BssDirector.PounceRecoverFrames, 0f, 1f));
            ctx.LegCommand = BssLegCommand.March;
            DeclareRoarHold(ctx, t, 16);

            Timer++;
            if (t >= BssDirector.PounceRecoverFrames) {
                if (pounces < BssDirector.PounceReps(ctx.Phase)) {
                    SwitchPhase(PouncePhase.Stalk);
                    return null;
                }
                return EndAttack(ctx);
            }
            return null;
        }
    }
}
