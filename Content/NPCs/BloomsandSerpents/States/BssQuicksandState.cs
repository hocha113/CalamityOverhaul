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
    /// 流沙陷阱（P2 起）：扎沙潜到玩家脚下 → 以玩家原位为圈心的沙面开始向心流动
    /// （圈缘流沙尘声明范围、圈心隆包渐鼓、隆隆加急）→ 陷在沙里的玩家被拉向圈心 →
    /// 拉沙结束的一刻破土直上 + 200 度沙弹扇。
    /// 沙漠本身成了它的嘴：躲法是趁早走出圈，或者干脆离地（拉力只对贴地的人生效）。
    /// 公平阀声明：圈心在潜入帧锁定不追人；拉力峰值 QuickPullForce 低于步行加速，早走轻松晚走吃紧；
    /// 离地 QuickPullHeight 以上不受拉（起跳即脱困）；隆包 = 破土倒计时；喷发扇两侧贴地留逃生道。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.Quicksand, typeof(BssStateContext))]
    internal class BssQuicksandState : BssStateBase
    {
        public override string StateName => "Quicksand";
        public override BssStateIndex StateIndex => BssStateIndex.Quicksand;

        private enum QuickPhase
        {
            Dive,    //前摇扎沙
            Sink,    //潜到圈心正下
            Pull,    //向心流沙
            Erupt,   //破土直上 + 回落
            Recover, //收势
        }

        private QuickPhase phase;
        /// <summary>圈心（地表点，潜入帧锁定）</summary>
        private Vector2 center;
        private float prevY;
        private bool diveFxDone;
        private bool eruptFxDone;
        private bool landed;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phase = QuickPhase.Dive;
            diveFxDone = false;
            eruptFxDone = false;
            landed = false;
            prevY = ctx.Npc.Center.Y;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case QuickPhase.Dive:
                    UpdateDive(ctx, npc);
                    break;
                case QuickPhase.Sink:
                    UpdateSink(ctx, npc);
                    break;
                case QuickPhase.Pull:
                    UpdatePull(ctx, npc);
                    break;
                case QuickPhase.Erupt:
                    UpdateErupt(ctx, npc);
                    break;
                case QuickPhase.Recover: {
                    IBssState next = UpdateRecover(ctx, npc);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
            }
            prevY = npc.Center.Y;

            //超时保险兜底
            if (Counter++ > 60 * 6) {
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(QuickPhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>前摇扎沙，入土帧锁定圈心（玩家脚下地表）</summary>
        private void UpdateDive(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlSpeed = 8f;
            ctx.CrawlDirX = FacingToTarget(ctx);
            if (t > 3) {
                ctx.LegCommand = BssLegCommand.Tuck;
                DeclareJaw(ctx, BssJawCommand.Clamp);
            }

            Timer++;
            if (t >= BssDirector.QuickDiveFrames) {
                float g = BssVfx.FindGroundY(new Vector2(ctx.Target.Center.X, ctx.Target.Center.Y - 300f), 1200f);
                center = new Vector2(ctx.Target.Center.X, g);
                if (!VaultUtils.isClient) {
                    npc.velocity = new Vector2(FacingToTarget(ctx, 0f) * 6f, 17f);
                    npc.netUpdate = true;
                }
                SwitchPhase(QuickPhase.Sink);
            }
        }

        /// <summary>潜行：地下鱼雷贴到圈心正下深处；到位或超时即起拉沙</summary>
        private void UpdateSink(BssStateContext ctx, NPC npc) {
            ctx.LegCommand = BssLegCommand.Tuck;
            DeclareJaw(ctx, BssJawCommand.Clamp);
            ctx.Mode = BssMoveMode.Steer;
            ctx.MoveTarget = center + new Vector2(0f, BssDirector.QuickDepth);
            ctx.MoveSpeed = BssDirector.LungeDigSpeed;
            ctx.TurnSpeed = 3f;
            ctx.AccelRate = 0.14f;
            UpdateDiveFx(ctx, npc);

            Timer++;
            bool arrived = Vector2.Distance(npc.Center, ctx.MoveTarget) < 70f;
            if ((arrived && Timer > 10) || Timer > 45) {
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), center - new Vector2(0f, 4f), Vector2.Zero,
                        ModContent.ProjectileType<BssBreachOmen>(), 0, 0f, Main.myPlayer, BssDirector.QuickPullFrames);
                    npc.netUpdate = true;
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.8f, Pitch = -0.7f, MaxInstances = 2 }, center);
                }
                SwitchPhase(QuickPhase.Pull);
            }
        }

        /// <summary>
        /// 向心流沙：头钉在圈心下蓄势；地表沙粒从圈缘流向圈心（圈缘密、圈心稀 = 范围声明），
        /// 本地玩家贴地在圈内即被拉；隆隆随进度加急。
        /// </summary>
        private void UpdatePull(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            float progress = MathHelper.Clamp(t / (float)BssDirector.QuickPullFrames, 0f, 1f);
            ctx.LegCommand = BssLegCommand.Tuck;
            ctx.Mode = BssMoveMode.Direct;
            Vector2 hold = center + new Vector2(0f, BssDirector.QuickDepth * (1f - 0.25f * progress));
            npc.velocity = Vector2.Lerp(npc.velocity, (hold - npc.Center) * 0.15f, 0.3f);
            npc.rotation = npc.rotation.AngleLerp(-MathHelper.PiOver2 + BssHead.FacingRot, 0.1f);
            ctx.GatherLevel = progress;
            ctx.Compression = MathHelper.Lerp(1f, 0.88f, progress);
            DeclareJaw(ctx, BssJawCommand.Inhale, progress);
            ctx.StormLevel = Math.Max(ctx.StormLevel, 0.9f + 0.1f * progress);

            if (!Main.dedServ) {
                float radius = BssDirector.QuickRadius;
                //圈缘流沙：从圈缘起步向圈心流，密度随进度上量
                int count = 3 + (int)(progress * 5f);
                for (int i = 0; i < count; i++) {
                    float r = radius * MathF.Sqrt(Main.rand.NextFloat(0.35f, 1f));
                    float side = Main.rand.NextBool() ? 1f : -1f;
                    float x = center.X + side * r;
                    float g = BssVfx.FindGroundY(new Vector2(x, center.Y - 160f), 500f);
                    Vector2 pos = new(x, g - 3f);
                    float inward = -side * (1.4f + 2.6f * progress) * (0.5f + 0.5f * (r / radius));
                    Dust d = Dust.NewDustPerfect(pos, DustID.Sand, new Vector2(inward, -Main.rand.NextFloat(0.2f, 1f)),
                        110, default, Main.rand.NextFloat(0.9f, 1.3f));
                    d.noGravity = true;
                }
                //圈缘标记：缘上一圈翻起的沙粒（范围读数）
                if (Main.rand.NextBool(2)) {
                    float side = Main.rand.NextBool() ? 1f : -1f;
                    float x = center.X + side * radius;
                    float g = BssVfx.FindGroundY(new Vector2(x, center.Y - 160f), 500f);
                    Dust rim = Dust.NewDustPerfect(new Vector2(x + Main.rand.NextFloat(-8f, 8f), g - 4f), DustID.Sand,
                        new Vector2(-side * 0.6f, -Main.rand.NextFloat(1.5f, 3f)), 90, default, Main.rand.NextFloat(1.2f, 1.6f));
                    rim.noGravity = false;
                }
                if (t % (progress > 0.6f ? 6 : 11) == 0) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.45f + 0.4f * progress, Pitch = -0.6f + 0.5f * progress, MaxInstances = 3 }, center);
                    BssVfx.Shake(center, 1.2f + 3.5f * progress, 1200f);
                }

                PullLocalPlayer(progress);
            }

            Timer++;
            if (t >= BssDirector.QuickPullFrames) {
                if (!VaultUtils.isClient) {
                    npc.velocity = new Vector2(MathHelper.Clamp((center.X - npc.Center.X) * 0.05f, -3f, 3f), -BssDirector.QuickEruptSpeed);
                    npc.netUpdate = true;
                }
                ctx.PulseGapWave(SerpentChainMath.WaveRelease, 0.16f);
                eruptFxDone = false;
                landed = false;
                SwitchPhase(QuickPhase.Erupt);
            }
        }

        /// <summary>拉沙只对本地玩家：贴地（离圈心地表 QuickPullHeight 内）且在圈内才受拉，越近圈心越紧</summary>
        private void PullLocalPlayer(float progress) {
            Player local = Main.LocalPlayer;
            if (!local.Alives()) {
                return;
            }
            float dx = local.Center.X - center.X;
            if (Math.Abs(dx) > BssDirector.QuickRadius) {
                return;
            }
            float g = BssVfx.FindGroundY(new Vector2(local.Center.X, local.Center.Y - 160f), 500f);
            if (g - local.Bottom.Y > BssDirector.QuickPullHeight) {
                return;
            }
            float closeness = 1f - Math.Abs(dx) / BssDirector.QuickRadius;
            float force = BssDirector.QuickPullForce * (0.4f + 0.6f * closeness) * (0.5f + 0.5f * progress);
            if (Math.Abs(dx) > 12f) {
                local.velocity.X -= Math.Sign(dx) * force;
            }
        }

        /// <summary>破土直上：穿面帧沙爆 + 吼 + 200 度沙弹扇；抛物回落贴面即落地</summary>
        private void UpdateErupt(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            ctx.Mode = BssMoveMode.Direct;
            ctx.LegCommand = BssLegCommand.Flail;
            npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y + 0.7f, -32f, 20f);
            npc.rotation = new Vector2(npc.velocity.X * 0.2f, npc.velocity.Y).ToRotation() + BssHead.FacingRot;
            DeclareJaw(ctx, BssJawCommand.Gape);

            float speed = npc.velocity.Length();
            if (speed > BssDirector.LungeContactSpeed) {
                npc.damage = npc.defDamage;
                DeclareSnatchIfClose(ctx, npc, BssDirector.LungeContactSpeed);
            }

            float g = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 340f), 1000f);
            if (!eruptFxDone && npc.velocity.Y < -6f && prevY > g && npc.Center.Y <= g + 24f) {
                eruptFxDone = true;
                ctx.PulseWhip(12f);
                if (!Main.dedServ) {
                    BssVfx.SandBurst(new Vector2(npc.Center.X, g), 2f);
                    BssVfx.Roar(npc.Center, -0.5f, 1.1f);
                    BssVfx.Shake(npc.Center, 9f, 1500f);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = -0.6f, MaxInstances = 2 }, npc.Center);
                }
                BssVfx.BreachEruption(npc, new Vector2(npc.Center.X, g), BssDirector.QuickEruptGlobs);
            }

            Timer++;
            //落地：下坠中贴到地表
            if (t > 10 && npc.velocity.Y > 0f && npc.Center.Y >= g - BssDirector.CrawlRideHeight) {
                landed = true;
            }
            if (landed || t > 110) {
                npc.velocity = new Vector2(npc.velocity.X * 0.2f, 0f);
                ctx.PulseGapWave(SerpentChainMath.WavePress, 0.12f);
                for (int k = 0; k < ctx.StationBob.Length; k++) {
                    ctx.StationBob[k] = 1f;
                }
                if (!Main.dedServ) {
                    BssVfx.SandBurst(new Vector2(npc.Center.X, g), 1.2f);
                    BssVfx.Shake(npc.Center, 5f);
                }
                SwitchPhase(QuickPhase.Recover);
            }
        }

        /// <summary>收势：吼一拍起身回爬</summary>
        private IBssState UpdateRecover(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = MathHelper.Lerp(0f, BssDirector.CrawlCruiseSpeed, MathHelper.Clamp(t / 16f, 0f, 1f));
            ctx.LegCommand = BssLegCommand.March;
            DeclareRoarHold(ctx, t, 16);

            Timer++;
            if (t >= 18) {
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>入土沙爆（各端本地）</summary>
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
