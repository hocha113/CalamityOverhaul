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
    /// 横风花刃（P2 起）：爬到玩家上风侧 → 前身立起吸气（风沙向嘴收束）→ 怒吼掀起横风：
    /// 全身红花被风撕下花瓣，花瓣顺风并入三条声明高度的车道横扫过玩家地带，横风轻推玩家。
    /// 与抖擞花瓣是两种读法：那是从上落下的幕，这是横着扫过的刃。
    /// 公平阀声明：车道高度表 GaleLaneHeights（站立被低道打，起跳的高度带正落在低中道之间的净空）；
    /// 花刃直飞不追踪、速度 GalePetalSpeed 给约 50 帧反应；三波之间 GaleBurstGap 帧的呼吸；
    /// 蛇先绕到上风侧（就位本身即预告，风向从入场起恒定）；推力小于步行速，不控场。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.WindBlade, typeof(BssStateContext))]
    internal class BssWindBladeState : BssStateBase
    {
        public override string StateName => "WindBlade";
        public override BssStateIndex StateIndex => BssStateIndex.WindBlade;

        private enum GalePhase
        {
            Approach, //绕到上风侧
            Raise,    //立起吸气
            Gale,     //怒吼横风 + 花刃波
            Recover,  //收势
        }

        private GalePhase phase;
        private Vector2 anchor;
        private float groundY;
        /// <summary>车道基准地表（横风起手帧按玩家脚下锁定）</summary>
        private float laneGround;
        private int burstsFired;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phase = GalePhase.Approach;
            burstsFired = 0;
            ctx.RefreshSegments();
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case GalePhase.Approach:
                    UpdateApproach(ctx, npc);
                    break;
                case GalePhase.Raise:
                    UpdateRaise(ctx, npc);
                    break;
                case GalePhase.Gale:
                    UpdateGale(ctx, npc);
                    break;
                case GalePhase.Recover: {
                    IBssState next = UpdateRecover(ctx, npc);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
            }

            //超时保险兜底
            if (Counter++ > 60 * 5) {
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(GalePhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>就位：爬到玩家上风侧 GaleUpwindOffset 处（花瓣要顺风扫向玩家）</summary>
        private void UpdateApproach(BssStateContext ctx, NPC npc) {
            float targetX = ctx.Target.Center.X - ctx.WindSign * BssDirector.GaleUpwindOffset;
            float dx = targetX - npc.Center.X;
            ctx.Mode = BssMoveMode.Crawl;
            ctx.LegCommand = BssLegCommand.March;
            float dir = Math.Sign(dx);
            ctx.CrawlDirX = dir != 0f ? dir : ctx.CrawlDirX;
            float ease = MathHelper.Clamp(Math.Abs(dx) / BssDirector.PatrolSlowBand, 0f, 1f);
            ctx.CrawlSpeed = MathHelper.Lerp(BssDirector.CrawlTurnSpeed, BssDirector.CrawlChaseSpeed, ease);

            Timer++;
            if (Math.Abs(dx) < 90f || Timer >= BssDirector.GaleApproachFrames) {
                anchor = npc.Center;
                groundY = BssVfx.FindGroundY(anchor - new Vector2(0f, 60f));
                if (!Main.dedServ) {
                    BssVfx.Roar(npc.Center, -0.2f, 0.6f);
                }
                SwitchPhase(GalePhase.Raise);
            }
        }

        /// <summary>立起吸气：前身昂起面朝下风侧，风沙向嘴收束，红花预亮</summary>
        private void UpdateRaise(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            float raise = MathHelper.Clamp(t / (float)BssDirector.GaleRaiseFrames, 0f, 1f);
            raise = raise * raise * (3f - 2f * raise);
            PoseRaised(ctx, npc, raise);
            DeclareJaw(ctx, BssJawCommand.Inhale, raise);
            ctx.BloomGlow = Math.Max(ctx.BloomGlow, raise * 0.7f);

            if (!Main.dedServ && Main.GameUpdateCount % 2 == 0) {
                Vector2 mouth = BssClawScript.MouthPos(npc.Center, npc.rotation, npc.scale);
                Vector2 from = mouth + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(40f, 100f);
                Dust d = Dust.NewDustPerfect(from, DustID.Sand, (mouth - from) * 0.11f, 120, default, 1f);
                d.noGravity = true;
            }

            Timer++;
            if (t >= BssDirector.GaleRaiseFrames) {
                laneGround = BssVfx.FindGroundY(new Vector2(ctx.Target.Center.X, ctx.Target.Center.Y - 300f), 1200f);
                ctx.PulseWhip(9f);
                if (!Main.dedServ) {
                    BssVfx.Roar(npc.Center, -0.55f, 1.1f);
                    BssVfx.Shake(npc.Center, 7f, 1500f);
                }
                SwitchPhase(GalePhase.Gale);
            }
        }

        /// <summary>立起姿态：X 定桩，头朝下风侧昂起</summary>
        private void PoseRaised(BssStateContext ctx, NPC npc, float raise) {
            ctx.Mode = BssMoveMode.Direct;
            ctx.LegCommand = BssLegCommand.Raise;
            ctx.FrontRaise = raise * 0.9f;
            ctx.Compression = MathHelper.Lerp(1f, 0.9f, raise);
            Vector2 pose = new(anchor.X, groundY - BssDirector.CrawlRideHeight - 160f * raise);
            Vector2 desired = (pose - npc.Center) * 0.1f;
            if (desired.Length() > 8f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 8f;
            }
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.25f);
            npc.rotation = npc.rotation.AngleLerp(new Vector2(ctx.WindSign * 0.75f, -1f).ToRotation() + BssHead.FacingRot, 0.14f);
        }

        /// <summary>
        /// 横风：沙暴拉满、屏幕横穿沙幕加密、本地玩家轻推；按拍从红花撕下花刃并入车道。
        /// 花刃波表现各端本地，实体权威端。
        /// </summary>
        private void UpdateGale(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            int wind = ctx.WindSign;
            PoseRaised(ctx, npc, 1f);
            DeclareRoarHold(ctx, t, BssDirector.GaleWindFrames);
            ctx.StormLevel = Math.Max(ctx.StormLevel, 1f);
            ctx.BloomGlow = Math.Max(ctx.BloomGlow, 0.5f);

            if (!Main.dedServ) {
                //加密的横穿沙幕（叠在头部常态风沙之上）
                for (int i = 0; i < 3; i++) {
                    Vector2 pos = Main.screenPosition + new Vector2(
                        Main.rand.NextFloat(-100f, Main.screenWidth + 100f), Main.rand.NextFloat(0f, Main.screenHeight));
                    Dust d = Dust.NewDustPerfect(pos, DustID.Sand,
                        new Vector2(wind * Main.rand.NextFloat(14f, 22f), Main.rand.NextFloat(-0.5f, 0.5f)),
                        Main.rand.Next(120, 190), default, Main.rand.NextFloat(1.4f, 2.2f));
                    d.noGravity = true;
                    d.fadeIn = 1.1f;
                }
                //嘴前吐风：沙流顺风喷出
                Vector2 mouth = BssClawScript.MouthPos(npc.Center, npc.rotation, npc.scale);
                Dust gust = Dust.NewDustPerfect(mouth + Main.rand.NextVector2Circular(10f, 10f), DustID.Sand,
                    new Vector2(wind * Main.rand.NextFloat(8f, 14f), Main.rand.NextFloat(-2f, 1f)), 110, default, 1.2f);
                gust.noGravity = true;

                //横风轻推本地玩家（不控场：已有横速时不再加）
                Player local = Main.LocalPlayer;
                if (local.Alives() && Math.Abs(local.Center.X - npc.Center.X) < 1800f
                    && Math.Abs(local.velocity.X) < 6f) {
                    local.velocity.X += wind * BssDirector.GalePushForce;
                }
                if (t % 18 == 0) {
                    BssVfx.Shake(npc.Center, 1.5f, 1400f);
                }
            }

            //花刃波
            if (burstsFired < BssDirector.GaleBursts && t == BssDirector.GaleRoarLead + burstsFired * BssDirector.GaleBurstGap) {
                burstsFired++;
                FireBurst(ctx, npc, wind);
            }

            Timer++;
            if (t >= BssDirector.GaleWindFrames) {
                SwitchPhase(GalePhase.Recover);
            }
        }

        /// <summary>
        /// 一波花刃：露地红花按链序轮转分配三条车道，每朵撕下 GalePetalsPerFlower 瓣顺风并道横飞。
        /// 车道 Y = 锁定地表 − GaleLaneHeights[车道]。
        /// </summary>
        private void FireBurst(BssStateContext ctx, NPC npc, int wind) {
            ctx.JawBurst = 1f;
            ctx.ShakeStrength = 0.6f;
            ctx.PulseWhip(6f);
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = 0.1f, MaxInstances = 3 }, npc.Center);
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 3 }, npc.Center);
            }

            int bodyType = ModContent.NPCType<BssBody>();
            int damage = BssDirector.ScaleProjectileDamage(npc, BssDirector.GalePetalDamage);
            int type = ModContent.ProjectileType<BssWindPetalProj>();
            int flowerSlot = 0;
            foreach (var seg in ctx.Segments) {
                if (!seg.Alives() || seg.type != bodyType) {
                    continue;
                }
                int ordinal = (int)seg.ai[0];
                if (!BssStateContext.IsFlowerOrdinal(ordinal) || !BssVfx.IsAboveGround(seg.Center)) {
                    continue;
                }
                int lane = flowerSlot % BssDirector.GaleLaneHeights.Length;
                flowerSlot++;
                float laneY = laneGround - BssDirector.GaleLaneHeights[lane];

                if (!Main.dedServ) {
                    for (int i = 0; i < 3; i++) {
                        BssVfx.PetalDrift(seg.Center + Main.rand.NextVector2Circular(12f, 12f),
                            new Vector2(wind * Main.rand.NextFloat(3f, 6f), -Main.rand.NextFloat(0.5f, 2f)));
                    }
                }
                if (VaultUtils.isClient) {
                    continue;
                }
                for (int i = 0; i < BssDirector.GalePetalsPerFlower; i++) {
                    Vector2 pos = seg.Center + Main.rand.NextVector2Circular(8f, 8f);
                    Vector2 vel = new(wind * Main.rand.NextFloat(3f, 5f), Main.rand.NextFloat(-1.5f, 0.5f));
                    float jitter = Main.rand.NextFloat(-BssDirector.GaleLaneHalfHeight, BssDirector.GaleLaneHalfHeight);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), pos, vel, type, damage, 0.4f, Main.myPlayer,
                        wind, laneY + jitter, Main.rand.NextFloat(MathHelper.TwoPi));
                }
            }
            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
            }
        }

        /// <summary>收势：落回爬行，压向玩家</summary>
        private IBssState UpdateRecover(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = MathHelper.Lerp(0f, BssDirector.CrawlCruiseSpeed, MathHelper.Clamp(t / 16f, 0f, 1f));
            ctx.LegCommand = BssLegCommand.March;
            ctx.FrontRaise = MathHelper.Clamp(0.9f - t / 16f, 0f, 1f);

            Timer++;
            if (t >= 18) {
                return EndAttack(ctx);
            }
            return null;
        }
    }
}
