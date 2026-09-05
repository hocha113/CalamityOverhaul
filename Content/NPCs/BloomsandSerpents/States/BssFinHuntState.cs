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
    /// 沙鳍追猎：扎进沙里 → 头贴着地表下方追玩家横位，地面上一道鳍浪（扬沙线 + 隆隆声）
    /// 跟着跑 → 追上或追够时脚下顶起隆包 → 张口竖直咬起 → 落回沙里再追。
    /// 与破土突袭的长跃不同：这是短竖直的一口，节奏靠"追上了没"驱动，玩家必须保持移动。
    /// 公平阀声明：追踪横速略高于步行、锁定要连续 FinLockFrames 帧对齐（跑动中难被锁 =
    /// 移动即答案）；追不上则超时就地咬（咬的是玩家原位）；每咬前 FinBulgeFrames 帧隆包预告；
    /// 伤害窗 = 速度门槛，咬到弧顶就闭口下落。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.FinHunt, typeof(BssStateContext))]
    internal class BssFinHuntState : BssStateBase
    {
        public override string StateName => "FinHunt";
        public override BssStateIndex StateIndex => BssStateIndex.FinHunt;

        private enum FinPhase
        {
            Dive,   //前摇扎沙
            Track,  //沙下追踪
            Bulge,  //隆包预告
            Bite,   //竖直咬起 + 回落
            Finish, //出面交还
        }

        private FinPhase phase;
        private int bites;
        private int alignedFrames;
        /// <summary>锁定咬点（隆包位置，咬起不再追瞄）</summary>
        private float lockX;
        private float lockGroundY;
        private bool apexSnapped;
        private float prevY;
        private bool diveFxDone;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phase = FinPhase.Dive;
            bites = 0;
            alignedFrames = 0;
            apexSnapped = false;
            diveFxDone = false;
            prevY = ctx.Npc.Center.Y;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case FinPhase.Dive:
                    UpdateDive(ctx, npc);
                    break;
                case FinPhase.Track:
                    UpdateTrack(ctx, npc);
                    break;
                case FinPhase.Bulge:
                    UpdateBulge(ctx, npc);
                    break;
                case FinPhase.Bite:
                    UpdateBite(ctx, npc);
                    break;
                case FinPhase.Finish: {
                    IBssState next = UpdateFinish(ctx, npc);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
            }
            prevY = npc.Center.Y;

            //超时保险兜底
            if (Counter++ > 60 * 9) {
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(FinPhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>前摇扎沙：收腿带冲势下探</summary>
        private void UpdateDive(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlSpeed = 9f;
            ctx.CrawlDirX = FacingToTarget(ctx);
            if (t > 2) {
                ctx.LegCommand = BssLegCommand.Tuck;
                DeclareJaw(ctx, BssJawCommand.Clamp);
            }

            Timer++;
            if (t >= 8) {
                if (!VaultUtils.isClient) {
                    npc.velocity = new Vector2(FacingToTarget(ctx, 0f) * 6f, 17f);
                    npc.netUpdate = true;
                }
                SwitchPhase(FinPhase.Track);
            }
        }

        /// <summary>
        /// 沙下追踪：贴地表下 FinDepth 深度横向追玩家，地面鳍浪扬沙 + 隆隆声。
        /// 连续对齐 FinLockFrames 帧或追满 FinTrackMaxFrames 即锁点。
        /// </summary>
        private void UpdateTrack(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            ctx.LegCommand = BssLegCommand.Tuck;
            DeclareJaw(ctx, BssJawCommand.Clamp);
            ctx.Mode = BssMoveMode.Direct;

            float dx = ctx.Target.Center.X - npc.Center.X;
            //地表从头与玩家二者更高处上方起扫，起点落在沙里会把坡体误读成地表
            float scanTop = Math.Min(ctx.Target.Center.Y, npc.Center.Y) - 420f;
            float surfaceY = BssVfx.FindGroundY(new Vector2(npc.Center.X, scanTop), 1400f);
            float desiredY = surfaceY + BssDirector.FinDepth;
            //前 6 帧还在下潜，之后才开始横追
            float targetVx = t < 6 ? npc.velocity.X * 0.8f : Math.Sign(dx) * BssDirector.FinTrackSpeed;
            if (Math.Abs(dx) < BssDirector.FinLockBand * 0.5f) {
                targetVx *= Math.Abs(dx) / (BssDirector.FinLockBand * 0.5f);
            }
            float vx = MathHelper.Lerp(npc.velocity.X, targetVx, 0.16f);
            float vy = MathHelper.Clamp((desiredY - npc.Center.Y) * 0.12f, -9f, 9f);
            npc.velocity = new Vector2(vx, vy);

            UpdateDiveFx(ctx, npc);
            EmitFinWake(npc, surfaceY, t);

            alignedFrames = Math.Abs(dx) < BssDirector.FinLockBand ? alignedFrames + 1 : 0;
            Timer++;
            if ((alignedFrames >= BssDirector.FinLockFrames && t > 12) || t >= BssDirector.FinTrackMaxFrames) {
                lockX = npc.Center.X;
                lockGroundY = surfaceY;
                alignedFrames = 0;
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), new Vector2(lockX, lockGroundY - 4f), Vector2.Zero,
                        ModContent.ProjectileType<BssBreachOmen>(), 0, 0f, Main.myPlayer, BssDirector.FinBulgeFrames);
                    npc.netUpdate = true;
                }
                SwitchPhase(FinPhase.Bulge);
            }
        }

        /// <summary>鳍浪（各端本地）：头顶正上方地表扬起一线沙尘，碎石弹跳，隆隆声按节拍</summary>
        private static void EmitFinWake(NPC npc, float surfaceY, int t) {
            if (Main.dedServ || t < 4) {
                return;
            }
            float speed = Math.Abs(npc.velocity.X);
            Vector2 fin = new(npc.Center.X, surfaceY - 4f);
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(fin + new Vector2(Main.rand.NextFloat(-22f, 22f), 0f), DustID.Sand,
                    new Vector2(-npc.velocity.X * 0.25f + Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(2f, 5f)),
                    90, default, Main.rand.NextFloat(1.1f, 1.6f));
                d.noGravity = false;
            }
            if (Main.rand.NextBool(3)) {
                Dust stone = Dust.NewDustPerfect(fin + new Vector2(Main.rand.NextFloat(-14f, 14f), -2f), DustID.Dirt,
                    new Vector2(-npc.velocity.X * 0.2f, -Main.rand.NextFloat(2f, 4f)), 80, default, 0.9f);
                stone.noGravity = false;
            }
            if (t % 11 == 0) {
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.55f, Pitch = -0.45f + speed * 0.02f, MaxInstances = 3 }, fin);
                BssVfx.Shake(fin, 1.6f, 800f);
            }
        }

        /// <summary>隆包预告：头钉在咬点正下方蓄势聚拢，颚从合死渐张到大开</summary>
        private void UpdateBulge(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            float progress = MathHelper.Clamp(t / (float)BssDirector.FinBulgeFrames, 0f, 1f);
            ctx.LegCommand = BssLegCommand.Tuck;
            ctx.Mode = BssMoveMode.Direct;
            Vector2 hold = new(lockX, lockGroundY + BssDirector.FinDepth * 0.85f);
            npc.velocity = Vector2.Lerp(npc.velocity, (hold - npc.Center) * 0.2f, 0.4f);
            npc.rotation = npc.rotation.AngleLerp(-MathHelper.PiOver2 + BssHead.FacingRot, 0.2f);
            ctx.GatherLevel = progress;
            DeclareJaw(ctx, BssJawCommand.Bite, progress);

            Timer++;
            if (t >= BssDirector.FinBulgeFrames) {
                if (!VaultUtils.isClient) {
                    float dx = MathHelper.Clamp((lockX - npc.Center.X) * 0.05f, -3f, 3f);
                    npc.velocity = new Vector2(dx, -BssDirector.FinBiteSpeed);
                    npc.netUpdate = true;
                }
                ctx.PulseGapWave(SerpentChainMath.WaveRelease, 0.14f);
                apexSnapped = false;
                SwitchPhase(FinPhase.Bite);
            }
        }

        /// <summary>咬起：直上张口，弧顶闭口（咬合帧），落回沙里进下一轮</summary>
        private void UpdateBite(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            ctx.Mode = BssMoveMode.Direct;
            ctx.LegCommand = BssLegCommand.Flail;
            npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y + BssDirector.FinBiteGravity, -30f, 20f);
            //竖直穿面：航向锁竖直，不随微小横速抖头
            npc.rotation = new Vector2(npc.velocity.X * 0.15f, npc.velocity.Y).ToRotation() + BssHead.FacingRot;

            float speed = npc.velocity.Length();
            bool rising = npc.velocity.Y < 0f;
            if (speed > BssDirector.FinContactSpeed) {
                npc.damage = npc.defDamage;
                DeclareSnatchIfClose(ctx, npc, BssDirector.FinContactSpeed);
            }
            if (rising) {
                DeclareJaw(ctx, BssJawCommand.Bite, 1f);
            }
            else {
                DeclareJaw(ctx, BssJawCommand.Bite, 0f);
                if (!apexSnapped) {
                    apexSnapped = true;
                    ctx.PulseWhip(7f);
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item102 with { Volume = 0.8f, Pitch = -0.5f, MaxInstances = 3 }, npc.Center);
                    }
                }
            }

            //出土瞬间
            float g = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 340f), 1000f);
            if (rising && prevY > g && npc.Center.Y <= g + 24f) {
                ctx.PulseWhip(11f);
                for (int k = 0; k < ctx.StationBob.Length; k++) {
                    ctx.StationBob[k] = 0.8f;
                }
                if (!Main.dedServ) {
                    BssVfx.SandBurst(new Vector2(npc.Center.X, g), 1.5f);
                    BssVfx.Roar(npc.Center, -0.35f, 0.9f);
                    BssVfx.Shake(npc.Center, 6f);
                }
            }
            //回落入沙
            if (!rising && prevY < g && npc.Center.Y >= g - 10f) {
                if (!Main.dedServ) {
                    BssVfx.SandBurst(new Vector2(npc.Center.X, g), 1f);
                    BssVfx.Shake(npc.Center, 3f);
                }
            }

            Timer++;
            if ((!rising && npc.Center.Y > g + 60f) || t > 90) {
                bites++;
                if (bites >= BssDirector.FinBites(ctx.Phase)) {
                    SwitchPhase(FinPhase.Finish);
                }
                else {
                    diveFxDone = true;
                    SwitchPhase(FinPhase.Track);
                }
            }
        }

        /// <summary>出面交还：钻到玩家近侧浅层抬头出面</summary>
        private IBssState UpdateFinish(BssStateContext ctx, NPC npc) {
            ctx.LegCommand = BssLegCommand.Tuck;
            DeclareJaw(ctx, BssJawCommand.Clamp);
            ctx.Mode = BssMoveMode.Steer;
            float side = Math.Sign(npc.Center.X - ctx.Target.Center.X);
            if (side == 0f) {
                side = 1f;
            }
            float exitX = ctx.Target.Center.X + side * 260f;
            float exitGround = BssVfx.FindGroundY(new Vector2(exitX, ctx.Target.Center.Y - 240f));
            ctx.MoveTarget = new Vector2(exitX, exitGround - BssDirector.CrawlRideHeight);
            ctx.MoveSpeed = 20f;
            ctx.TurnSpeed = 3f;
            ctx.AccelRate = 0.12f;

            float g = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 340f), 1000f);
            if (npc.velocity.Y < -4f && prevY > g && npc.Center.Y <= g + 24f) {
                ctx.PulseWhip(8f);
                if (!Main.dedServ) {
                    BssVfx.SandBurst(new Vector2(npc.Center.X, g), 1.3f);
                    BssVfx.Shake(npc.Center, 4f);
                }
            }

            Timer++;
            bool arrived = Vector2.Distance(npc.Center, ctx.MoveTarget) < 90f;
            if (arrived || Timer > 80) {
                npc.velocity *= 0.5f;
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>首次入土的沙爆（各端本地）</summary>
        private void UpdateDiveFx(BssStateContext ctx, NPC npc) {
            if (diveFxDone) {
                return;
            }
            float g = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 340f), 1000f);
            if (prevY < g && npc.Center.Y >= g - 10f) {
                diveFxDone = true;
                ctx.PulseWhip(8f);
                if (!Main.dedServ) {
                    BssVfx.SandBurst(new Vector2(npc.Center.X, g), 1.2f);
                    BssVfx.Shake(npc.Center, 3.5f);
                }
            }
        }
    }
}
