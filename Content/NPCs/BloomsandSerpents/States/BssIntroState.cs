using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 破土入场：沙面震动预告 → 破土冲天 → 落地四足撑起 → 凝视静止拍。
    /// 全程无伤害。威慑主要靠静止（PACING：入场的凶相多半是站定不动）。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.Intro, typeof(BssStateContext))]
    internal class BssIntroState : BssStateBase
    {
        public override string StateName => "Intro";
        public override BssStateIndex StateIndex => BssStateIndex.Intro;

        private const int BreachFrame = 52;
        private const int LandWatchFrom = 80;
        /// <summary>落地凝视静止拍</summary>
        private const int StareFrames = 28;

        /// <summary>本端上一帧头部 Y（破土/落地穿面检测，纯表现）</summary>
        private float prevY;
        private bool breachFxDone;
        private bool landed;
        private float landTime = -1f;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            ctx.Phase = 1;
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
                float breachX = ctx.Target.Center.X + side * 190f;
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
                //地下蓄势：钉住不动，预告实体负责地面演出
                ctx.Mode = BssMoveMode.Hold;
                npc.velocity = Vector2.Zero;
            }
            else if (t == BreachFrame) {
                //破土：一帧定初速（力量在出手帧，不在加速度）
                if (!VaultUtils.isClient && ctx.Target.Alives()) {
                    int side = Math.Sign(npc.Center.X - ctx.Target.Center.X);
                    if (side == 0) {
                        side = 1;
                    }
                    npc.velocity = new Vector2(-side * 3.2f, -BssDirector.BreachLaunchSpeed);
                    npc.netUpdate = true;
                }
                npc.alpha = 0;
                ctx.Mode = BssMoveMode.Direct;
            }
            else {
                //腾空抛物 → 落地
                ctx.Mode = BssMoveMode.Direct;
                ctx.LegCommand = landed ? BssLegCommand.March : BssLegCommand.Flail;
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
                        BssVfx.SandBurst(new Vector2(npc.Center.X, groundY), 1.1f);
                        BssVfx.Shake(npc.Center, 5f);
                    }
                }

                if (landed) {
                    //凝视静止拍：钉在原地，只留呼吸级抖动
                    ctx.Mode = BssMoveMode.Crawl;
                    ctx.CrawlSpeed = 0f;
                    ctx.CrawlDirX = FacingToTarget(ctx);
                    ctx.FrontRaise = MathHelper.Clamp((t - landTime) / 18f, 0f, 0.45f);
                    if (t - landTime == 12) {
                        BssVfx.Roar(npc.Center, -0.25f, 0.8f);
                    }
                    if (t - landTime > StareFrames) {
                        return EndStare(ctx);
                    }
                }
            }

            //体节链兜底：破土帧起权威端逐帧确保链在——旧实现只在破土那一帧且目标存活时生成一次，
            //目标恰在该帧无效（死亡/离场）就整场只剩头（反馈 #37）；
            //头的体节数槽当闩防重复生成（重复生成会翻倍统一血池）
            if (!VaultUtils.isClient && t >= BreachFrame
                && npc.ai[BssHead.SlotSegmentCount] <= 0) {
                BssHead.SpawnBodySegments(npc);
            }

            //破土穿面表现（各端本地按位置检测）
            if (!breachFxDone && t >= BreachFrame && !Main.dedServ) {
                float groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 300f), 900f);
                if (prevY > groundY && npc.Center.Y <= groundY + 20f) {
                    breachFxDone = true;
                    ctx.PulseWhip(12f);
                    BssVfx.SandBurst(new Vector2(npc.Center.X, groundY), 1.8f);
                    BssVfx.Roar(npc.Center, -0.5f, 1.1f);
                    BssVfx.Shake(npc.Center, 9f);
                    for (int i = 0; i < 10; i++) {
                        BssVfx.PetalDrift(npc.Center + Main.rand.NextVector2Circular(30f, 30f),
                            new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, 3f)));
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
            ctx.AttackCooldown = 24;
            return new BssHubState();
        }
    }
}
