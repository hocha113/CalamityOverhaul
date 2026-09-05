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
    /// 翻身掀浪：就地扎进沙里 → 沙下翻身（地表震颤、渗沙隆起、沙暴脉冲 = 预告）→
    /// 从翻身点掀出沿地行进的矮沙浪 → 蛇在浪后出土交还爬行。
    /// "沙暴起时就是它在下面翻身"的实体化：浪是翻身的可见结果。
    /// 公平阀声明：浪高 SurgeWaveHeight 低于单跳（原地起跳即安全）；浪速高于步行
    /// （逼跳不逼跑）；P1 单向朝玩家，P2 起双向；P3 第二浪延迟 SurgeSecondWaveDelay
    /// （落地再跳的二段节奏）。浪撞陡壁自崩。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.SandSurge, typeof(BssStateContext))]
    internal class BssSandSurgeState : BssStateBase
    {
        public override string StateName => "SandSurge";
        public override BssStateIndex StateIndex => BssStateIndex.SandSurge;

        private enum SurgePhase
        {
            Dive,   //前摇扎沙
            Roll,   //沙下翻身蓄势
            Emerge, //浪后出土
        }

        private SurgePhase phase;
        /// <summary>翻身点（入土帧锁定，浪由此掀出）</summary>
        private Vector2 rollPoint;
        private float groundY;
        private float toward = 1f;
        private int wavesLaunched;
        private float prevY;
        private bool diveFxDone;
        private bool emergeFxDone;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phase = SurgePhase.Dive;
            wavesLaunched = 0;
            diveFxDone = false;
            emergeFxDone = false;
            prevY = ctx.Npc.Center.Y;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case SurgePhase.Dive:
                    UpdateDive(ctx, npc);
                    break;
                case SurgePhase.Roll:
                    UpdateRoll(ctx, npc);
                    break;
                case SurgePhase.Emerge: {
                    IBssState next = UpdateEmerge(ctx, npc);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
            }
            prevY = npc.Center.Y;

            //超时保险兜底
            if (Counter++ > 60 * 5) {
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(SurgePhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>前摇：带着冲势收腿扎下去（腿收拢即入土信号）</summary>
        private void UpdateDive(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            toward = FacingToTarget(ctx, 0f);
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlSpeed = 8f;
            ctx.CrawlDirX = toward;
            if (t > 3) {
                ctx.LegCommand = BssLegCommand.Tuck;
                DeclareJaw(ctx, BssJawCommand.Clamp);
            }

            Timer++;
            if (t >= BssDirector.SurgeDiveFrames) {
                groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 80f), 600f);
                rollPoint = new Vector2(npc.Center.X, groundY);
                if (!VaultUtils.isClient) {
                    npc.velocity = new Vector2(toward * 4f, 17f);
                    npc.netUpdate = true;
                }
                SwitchPhase(SurgePhase.Roll);
            }
        }

        /// <summary>
        /// 翻身：头在翻身点下方盘一小圈（沙下看不见，地表看得见）——渗沙面越翻越宽、
        /// 震颤越翻越急、沙暴短促加剧；末帧掀浪。
        /// </summary>
        private void UpdateRoll(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            float progress = MathHelper.Clamp(t / (float)BssDirector.SurgeRollFrames, 0f, 1f);

            ctx.LegCommand = BssLegCommand.Tuck;
            DeclareJaw(ctx, BssJawCommand.Clamp);
            ctx.Mode = BssMoveMode.Steer;
            //沙下小圈：绕翻身点下方转，链在沙里绞成一团（地下航段，转弯半径不受地板约束）
            float ang = progress * MathHelper.TwoPi * 1.2f * toward;
            ctx.MoveTarget = rollPoint + new Vector2(MathF.Cos(ang) * BssDirector.SurgeRollRadius,
                BssDirector.SurgeRollDepth + MathF.Sin(ang) * BssDirector.SurgeRollRadius * 0.55f);
            ctx.MoveSpeed = 16f;
            ctx.TurnRadius = BssDirector.BuriedTurnRadius;
            ctx.AccelRate = 0.2f;
            ctx.GatherLevel = progress;
            ctx.Compression = MathHelper.Lerp(1f, 0.9f, progress);
            //沙暴短促加剧（翻身掀起的风），头部阶段底线之上叠一口
            ctx.StormLevel = Math.Max(ctx.StormLevel, MathHelper.Clamp(0.9f + 0.1f * progress, 0f, 1f));

            UpdateCrossFx(ctx, npc);

            if (!Main.dedServ) {
                //地表渗沙面随进度加宽、隆起碎石
                float halfWidth = 40f + 160f * progress;
                int count = 2 + (int)(progress * 4f);
                for (int i = 0; i < count; i++) {
                    Vector2 pos = new(rollPoint.X + Main.rand.NextFloat(-halfWidth, halfWidth), groundY - 3f);
                    Dust d = Dust.NewDustPerfect(pos, DustID.Sand,
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(1f, 2.5f + 4f * progress)),
                        100, default, Main.rand.NextFloat(0.9f, 1.4f));
                    d.noGravity = false;
                }
                if (progress > 0.5f && Main.rand.NextBool(3)) {
                    Dust stone = Dust.NewDustPerfect(new Vector2(rollPoint.X + Main.rand.NextFloat(-halfWidth, halfWidth), groundY - 6f),
                        DustID.Dirt, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(2f, 5f)), 80, default, 0.9f);
                    stone.noGravity = false;
                }
                if (t % (progress > 0.6f ? 5 : 9) == 0) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.5f + 0.4f * progress, Pitch = -0.6f + 0.4f * progress, MaxInstances = 3 },
                        rollPoint);
                    BssVfx.Shake(rollPoint, 1.5f + 4f * progress, 1100f);
                }
            }

            Timer++;
            if (t >= BssDirector.SurgeRollFrames) {
                LaunchWave(ctx, npc, first: true);
                SwitchPhase(SurgePhase.Emerge);
            }
        }

        /// <summary>
        /// 掀浪：翻身点两侧地表炸开，浪沿地朝玩家行进（P2 起双向）。
        /// 第二浪（P3）由出土段按延迟补掀。
        /// </summary>
        private void LaunchWave(BssStateContext ctx, NPC npc, bool first) {
            wavesLaunched++;
            ctx.PulseWhip(10f);
            ctx.PulseGapWave(SerpentChainMath.WaveRelease, 0.14f);
            float dirToPlayer = Math.Sign(ctx.Target.Center.X - rollPoint.X);
            if (dirToPlayer == 0f) {
                dirToPlayer = toward;
            }
            bool bothSides = ctx.Phase >= 2;

            if (!Main.dedServ) {
                BssVfx.SandBurst(rollPoint, first ? 2f : 1.4f);
                BssVfx.Roar(rollPoint, -0.55f, first ? 1f : 0.7f);
                BssVfx.Shake(rollPoint, first ? 9f : 6f, 1500f);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.7f, Pitch = -0.7f, MaxInstances = 2 }, rollPoint);
            }
            if (VaultUtils.isClient) {
                return;
            }
            int damage = BssDirector.ScaleProjectileDamage(npc, BssDirector.SurgeDamage);
            int type = ModContent.ProjectileType<BssSandSurgeProj>();
            for (int s = -1; s <= 1; s += 2) {
                if (!bothSides && s != dirToPlayer) {
                    continue;
                }
                Vector2 pos = new(rollPoint.X + s * BssDirector.SurgeLaunchOffset, groundY - 6f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), pos, new Vector2(s * BssDirector.SurgeWaveSpeed, 0f),
                    type, damage, 1f, Main.myPlayer, s, BssDirector.SurgeWaveTravelFrames);
            }
        }

        /// <summary>出土：钻到翻身点后侧浅层抬头出面（浪已跑远，蛇在浪后压上），P3 中途补第二浪</summary>
        private IBssState UpdateEmerge(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            ctx.LegCommand = BssLegCommand.Tuck;
            DeclareJaw(ctx, BssJawCommand.Clamp);

            if (wavesLaunched < BssDirector.SurgeWaves(ctx.Phase) && t == BssDirector.SurgeSecondWaveDelay) {
                LaunchWave(ctx, npc, first: false);
            }

            float side = Math.Sign(rollPoint.X - ctx.Target.Center.X);
            if (side == 0f) {
                side = -toward;
            }
            float exitX = rollPoint.X + side * 200f;
            float exitGround = BssVfx.FindGroundY(new Vector2(exitX, groundY - 260f), 900f);
            ctx.Mode = BssMoveMode.Steer;
            ctx.MoveTarget = new Vector2(exitX, exitGround - BssDirector.CrawlRideHeight);
            ctx.MoveSpeed = 18f;
            ctx.TurnRadius = BssDirector.EmergeTurnRadius;
            ctx.AccelRate = 0.12f;

            UpdateCrossFx(ctx, npc);
            Timer++;

            bool arrived = Vector2.Distance(npc.Center, ctx.MoveTarget) < 80f;
            bool wavesDone = wavesLaunched >= BssDirector.SurgeWaves(ctx.Phase);
            if ((arrived && wavesDone) || t >= BssDirector.SurgeEmergeFrames) {
                npc.velocity *= 0.5f;
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>穿面检测（各端本地）：入土沙爆、出土沙爆 + 吼</summary>
        private void UpdateCrossFx(BssStateContext ctx, NPC npc) {
            float g = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 340f), 1000f);
            if (!diveFxDone && prevY < g && npc.Center.Y >= g - 10f) {
                diveFxDone = true;
                ctx.PulseWhip(8f);
                if (!Main.dedServ) {
                    BssVfx.SandBurst(new Vector2(npc.Center.X, g), 1.2f);
                    BssVfx.Shake(npc.Center, 3.5f);
                }
            }
            if (!emergeFxDone && phase == SurgePhase.Emerge && npc.velocity.Y < -4f && prevY > g && npc.Center.Y <= g + 24f) {
                emergeFxDone = true;
                ctx.PulseWhip(9f);
                if (!Main.dedServ) {
                    BssVfx.SandBurst(new Vector2(npc.Center.X, g), 1.4f);
                    BssVfx.Roar(npc.Center, -0.3f, 0.8f);
                    BssVfx.Shake(npc.Center, 5f);
                }
            }
        }
    }
}
