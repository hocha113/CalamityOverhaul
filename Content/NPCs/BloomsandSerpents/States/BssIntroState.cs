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
    /// 破土入场：沙面隆起预告 → 轰然破土冲天（沙爆 + 声波环 + 重震）→ 破土瞬间掀起
    /// 全屏沙暴并在 <see cref="BssDirector.StormRiseFrames"/> 帧内拉满 → 落地四足撑起 →
    /// 凝视静止拍。全程无伤害。威慑主要靠破土那一记与随后的静止。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.Intro, typeof(BssStateContext))]
    internal class BssIntroState : BssStateBase
    {
        public override string StateName => "Intro";
        public override BssStateIndex StateIndex => BssStateIndex.Intro;

        private const int BreachFrame = 56;
        private const int LandWatchFrom = 84;
        /// <summary>落地凝视静止拍</summary>
        private const int StareFrames = 30;

        /// <summary>本端上一帧头部 Y（破土/落地穿面检测，纯表现）</summary>
        private float prevY;
        private bool breachFxDone;
        private bool landed;
        private float landTime = -1f;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            ctx.Phase = 1;
            ctx.StormLevel = 0f;
            breachFxDone = false;
            landed = false;
            landTime = -1f;
            prevY = ctx.Npc.Center.Y;

            //权威端：把头挪到玩家侧旁地下，预告实体钉在破土点
            if (!VaultUtils.isClient && ctx.Target.Alives()) {
                NPC npc = ctx.Npc;
                int side = Math.Sign(npc.Center.X - ctx.Target.Center.X);
                if (side == 0) {
                    side = 1;
                }
                float breachX = ctx.Target.Center.X + side * 220f;
                float groundY = BssVfx.FindGroundY(new Vector2(breachX, ctx.Target.Center.Y - 200f));
                npc.Center = new Vector2(breachX, groundY + 420f);
                npc.velocity = Vector2.Zero;
                npc.netUpdate = true;
                Projectile.NewProjectile(npc.GetSource_FromAI(), new Vector2(breachX, groundY - 4f),
                    Vector2.Zero, ModContent.ProjectileType<BssBreachOmen>(), 0, 0f, Main.myPlayer, BreachFrame);

                //开幕双柱：玩家两翼各起一根置景柱（无伤害、长滞留），预告期与蛇的
                //破土预告同拍隆隆，蛇出土后紧跟着立起——开幕即立"沙丘"招牌，
                //也是 P2 爆震首秀的燃料
                for (int flank = -1; flank <= 1; flank += 2) {
                    Vector2 anchor = ctx.Target.Center + new Vector2(flank * 440f, 0f);
                    BssSandPillar.Spawn(npc, anchor,
                        BssDirector.PillarHeightMax, BssDirector.PillarWidth,
                        BreachFrame + 10, BssDirector.PillarIntroLinger, armedPillar: false);
                }
            }
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            ctx.LegAlpha = 0f;
            ctx.LegCommand = BssLegCommand.Tuck;

            if (t < BreachFrame) {
                //地下蓄势：钉住不动，预告实体负责地面演出；临破前地面先起风。
                //头先在地下转到朝上：朝向限速下破土帧才不用边飞边翻身
                ctx.Mode = BssMoveMode.Hold;
                npc.velocity = Vector2.Zero;
                ctx.AimAngle = -MathHelper.PiOver2;
                DeclareJaw(ctx, BssJawCommand.Clamp);
                if (t > BreachFrame - 20) {
                    ctx.StormLevel = Math.Max(ctx.StormLevel, (t - (BreachFrame - 20)) / 20f * 0.18f);
                }
            }
            else if (t == BreachFrame) {
                //破土：一帧定初速（力量在出手帧，不在加速度）
                if (!VaultUtils.isClient && ctx.Target.Alives()) {
                    int side = Math.Sign(npc.Center.X - ctx.Target.Center.X);
                    if (side == 0) {
                        side = 1;
                    }
                    npc.velocity = new Vector2(-side * 3.4f, -BssDirector.BreachLaunchSpeed);
                    npc.netUpdate = true;
                }
                npc.alpha = 0;
                ctx.Mode = BssMoveMode.Direct;
                DeclareJaw(ctx, BssJawCommand.Gape);
            }
            else {
                //腾空抛物 → 落地
                ctx.Mode = BssMoveMode.Direct;
                ctx.LegCommand = landed ? BssLegCommand.March : BssLegCommand.Flail;
                if (!landed) {
                    DeclareJaw(ctx, BssJawCommand.Gape);
                }
                ctx.LegAlpha = landed ? 1f : 0.85f;
                if (!landed) {
                    npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y + BssDirector.LungeGravity, -30f, 18f);
                    npc.velocity.X *= 0.995f;
                }

                //落地判定：下落中贴近地表即撑住
                if (!landed && t > LandWatchFrom && npc.velocity.Y > 0f) {
                    float groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 60f));
                    if (npc.Center.Y >= groundY - BssDirector.CrawlRideHeight) {
                        landed = true;
                        landTime = t;
                        npc.velocity = new Vector2(npc.velocity.X * 0.2f, 0f);
                        for (int k = 0; k < ctx.StationBob.Length; k++) {
                            ctx.StationBob[k] = 1.1f;
                        }
                        BssVfx.SandBurst(new Vector2(npc.Center.X, groundY), 1.2f);
                        BssVfx.Shake(npc.Center, 5f);
                    }
                }

                if (landed) {
                    //凝视静止拍：钉在原地，只留呼吸级抖动，鳌足张开
                    ctx.Mode = BssMoveMode.Crawl;
                    ctx.CrawlSpeed = 0f;
                    ctx.CrawlDirX = FacingToTarget(ctx);
                    ctx.FrontRaise = MathHelper.Clamp((t - landTime) / 18f, 0f, 0.45f);
                    float stareAge = t - landTime;
                    ctx.ClawCommand = BssClawCommand.Brace;
                    ctx.ClawPhase = MathHelper.Clamp(stareAge / 16f, 0f, 1f);
                    if (stareAge == 12) {
                        BssVfx.Roar(npc.Center, -0.25f, 0.8f);
                    }
                    if (stareAge >= 12) {
                        DeclareRoarHold(ctx, (int)(stareAge - 12), 20);
                    }
                    if (t - landTime > StareFrames) {
                        return EndStare(ctx);
                    }
                }
            }

            //沙暴：破土瞬间掀起，StormRiseFrames 内拉满（此后由头部的阶段底线常驻）
            if (t >= BreachFrame) {
                float rise = MathHelper.Clamp((t - BreachFrame) / (float)BssDirector.StormRiseFrames, 0f, 1f);
                ctx.StormLevel = Math.Max(ctx.StormLevel, 0.3f + 0.7f * rise);
            }

            //体节链兜底：破土帧起权威端逐帧确保链在——目标恰在该帧无效（死亡/离场）
            //也不会整场只剩头；头的体节数槽当闩防重复生成（重复生成会翻倍统一血池）
            if (!VaultUtils.isClient && t >= BreachFrame
                && npc.ai[BssHead.SlotSegmentCount] <= 0) {
                BssHead.SpawnBodySegments(npc);
            }

            //破土穿面表现（各端本地按位置检测）：这是全场最大的一记
            if (!breachFxDone && t >= BreachFrame && !Main.dedServ) {
                float groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 300f), 900f);
                if (prevY > groundY && npc.Center.Y <= groundY + 20f) {
                    breachFxDone = true;
                    Vector2 ground = new(npc.Center.X, groundY);
                    ctx.PulseWhip(14f);
                    ctx.FireRoarRing(ground);
                    BssVfx.SandBurst(ground, 2.8f);
                    BssVfx.SandBurst(ground + new Vector2(-70f, 0f), 1.2f);
                    BssVfx.SandBurst(ground + new Vector2(70f, 0f), 1.2f);
                    BssVfx.Roar(npc.Center, -0.6f, 1.2f);
                    BssVfx.Roar(npc.Center, -0.15f, 0.8f);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.5f, MaxInstances = 2 }, ground);
                    BssVfx.Shake(npc.Center, 14f, 1800f);
                    //横向沙浪：破土的冲击把地面沙层向两侧掀开
                    for (int i = 0; i < 40; i++) {
                        float side = i % 2 == 0 ? 1f : -1f;
                        Dust d = Dust.NewDustPerfect(ground + new Vector2(side * Main.rand.NextFloat(10f, 80f), -4f),
                            DustID.Sand,
                            new Vector2(side * Main.rand.NextFloat(4f, 12f), -Main.rand.NextFloat(1f, 5f)),
                            Main.rand.Next(70, 130), default, Main.rand.NextFloat(1.2f, 2f));
                        d.noGravity = Main.rand.NextBool(3);
                    }
                    for (int i = 0; i < 14; i++) {
                        BssVfx.PetalDrift(npc.Center + Main.rand.NextVector2Circular(30f, 30f),
                            new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), -Main.rand.NextFloat(1f, 3.5f)));
                    }
                }
            }
            prevY = npc.Center.Y;

            Timer++;

            //超时保险：无论演到哪，入场不超过 4 秒
            if (t > 240) {
                return EndStare(ctx);
            }
            return null;
        }

        private static IBssState EndStare(BssStateContext ctx) {
            ctx.AttackCooldown = 30;
            return new BssHubState();
        }
    }
}
