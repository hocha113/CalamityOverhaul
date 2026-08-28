using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.States
{
    /// <summary>
    /// 入场（两拍演出，全程无伤）：
    /// 拍一「污染扩散」——破土点腐化污渍沿地表向两侧蔓延、灵液气泡从沙中鼓破，
    /// 隆包渗金越鼓越急；拍二「双弧破土」——冲天跃起跨过玩家头顶洒无伤灵液雨，
    /// 重砸落地立起怒吼，腐沙暴滤镜猛压。十秒内两记演出震撼由这两拍承担。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.Intro, typeof(FssStateContext))]
    internal class FssIntroState : FssStateBase
    {
        public override string StateName => "Intro";
        public override FssStateIndex StateIndex => FssStateIndex.Intro;

        private const int LandWatchFrom = 150;
        /// <summary>立起怒吼段帧数</summary>
        private const int RearFrames = 20;
        /// <summary>凝视静止拍</summary>
        private const int StareFrames = 26;

        /// <summary>本端上一帧头部 Y（破土/落地穿面检测，纯表现）</summary>
        private float prevY;
        private bool breachFxDone;
        private bool landed;
        private float landTime = -1f;

        public override void OnEnter(FssStateContext ctx) {
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
                float breachX = ctx.Target.Center.X + side * 220f;
                float groundY = FssVfx.FindGroundY(new Vector2(breachX, ctx.Target.Center.Y - 200f));
                npc.Center = new Vector2(breachX, groundY + 460f);
                npc.velocity = Vector2.Zero;
                npc.netUpdate = true;
                Projectile.NewProjectile(npc.GetSource_FromAI(), new Vector2(breachX, groundY - 4f),
                    Vector2.Zero, ModContent.ProjectileType<FssBreachOmen>(), 0, 0f, Main.myPlayer,
                    FssDirector.IntroBreachFrame);
            }
        }

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            ctx.LegAlpha = 0f;
            ctx.LegCommand = FssLegCommand.Tuck;

            if (t < FssDirector.IntroBreachFrame) {
                //地下蓄势：钉住不动，污染扩散演出铺开（拍一）
                ctx.Mode = FssMoveMode.Hold;
                npc.velocity = Vector2.Zero;
                ctx.StormLevel = Math.Max(ctx.StormLevel, 0.3f * t / FssDirector.IntroStainFrames);
                UpdateStainSpread(ctx, npc, t);
            }
            else if (t == FssDirector.IntroBreachFrame) {
                //破土：一帧定初速（力量在出手帧），高抛弧线跨越玩家
                if (!VaultUtils.isClient && ctx.Target.Alives()) {
                    int side = Math.Sign(npc.Center.X - ctx.Target.Center.X);
                    if (side == 0) {
                        side = 1;
                    }
                    npc.velocity = new Vector2(-side * FssDirector.IntroArcDriftX, -FssDirector.IntroLaunchSpeed);
                    npc.netUpdate = true;
                    FssHead.SpawnBodySegments(npc);
                }
                npc.alpha = 0;
                ctx.Mode = FssMoveMode.Direct;
            }
            else {
                //腾空抛物 → 落地（拍二主体）
                ctx.Mode = FssMoveMode.Direct;
                ctx.LegCommand = landed ? FssLegCommand.Raise : FssLegCommand.Flail;
                ctx.LegAlpha = landed ? 1f : 0.85f;
                if (!landed) {
                    npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y + 0.58f, -40f, 19f);
                    npc.velocity.X *= 0.995f;
                    //腾空洒灵液雨（无伤 dressing：巨物从头顶过、金雨随之落）
                    if (!Main.dedServ) {
                        for (int i = 0; i < 2; i++) {
                            Vector2 from = Main.rand.NextBool(3)
                                ? npc.Center
                                : npc.Center - npc.velocity * Main.rand.NextFloat(1f, 5f);
                            Dust drip = Dust.NewDustPerfect(from + Main.rand.NextVector2Circular(26f, 26f),
                                DustID.Ichor, new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1f, 3f)),
                                40, default, Main.rand.NextFloat(0.9f, 1.3f));
                            drip.noGravity = false;
                        }
                    }
                }

                //落地判定：下落中贴近地表即撑住
                if (!landed && t > LandWatchFrom && npc.velocity.Y > 0f) {
                    float groundY = FssVfx.FindGroundY(npc.Center - new Vector2(0f, 60f));
                    if (npc.Center.Y >= groundY - FssDirector.CrawlRideHeight) {
                        landed = true;
                        landTime = t;
                        npc.velocity = new Vector2(npc.velocity.X * 0.2f, 0f);
                        FssVfx.CorruptSandBurst(new Vector2(npc.Center.X, groundY), 1.5f);
                        FssVfx.IchorBurst(npc.Center, 1.2f);
                        FssVfx.Shake(npc.Center, 6f);
                        ctx.PulseWhip(10f);
                    }
                }

                if (landed) {
                    //立起怒吼 → 凝视：腐沙暴滤镜猛压 + 金雾冲击（拍二收束）
                    ctx.Mode = FssMoveMode.Crawl;
                    ctx.CrawlSpeed = 0f;
                    ctx.CrawlDirX = FacingToTarget(ctx);
                    ctx.LegCommand = FssLegCommand.Raise;
                    ctx.FrontRaise = MathHelper.Clamp((t - landTime) / (float)RearFrames, 0f, 1f);
                    ctx.StormLevel = Math.Max(ctx.StormLevel, MathHelper.Clamp(0.3f + (t - landTime) / 30f, 0f, 0.6f));
                    if (t - landTime == 12) {
                        FssVfx.Roar(npc.Center, -0.6f, 1.15f);
                        FssVfx.IchorBurst(npc.Center, 1.8f, -Vector2.UnitY);
                        FssVfx.Shake(npc.Center, 5f);
                        ctx.CystGlow = 1f;
                    }
                    if (t - landTime > RearFrames + StareFrames) {
                        return EndStare(ctx);
                    }
                }
            }

            //破土穿面表现（各端本地按位置检测）
            if (!breachFxDone && t >= FssDirector.IntroBreachFrame && !Main.dedServ) {
                float groundY = FssVfx.FindGroundY(npc.Center - new Vector2(0f, 300f), 900f);
                if (prevY > groundY && npc.Center.Y <= groundY + 20f) {
                    breachFxDone = true;
                    ctx.PulseWhip(13f);
                    FssVfx.CorruptSandBurst(new Vector2(npc.Center.X, groundY), 2f);
                    FssVfx.IchorBurst(new Vector2(npc.Center.X, groundY), 1.6f, -Vector2.UnitY);
                    FssVfx.Roar(npc.Center, -0.45f, 1.1f);
                    FssVfx.Shake(npc.Center, 9f);
                }
            }
            prevY = npc.Center.Y;

            Timer++;

            //超时保险：无论演到哪，入场不超过 5.5 秒
            if (t > 330) {
                return EndStare(ctx);
            }
            return null;
        }

        /// <summary>
        /// 拍一：污染扩散（客户端表现）。污渍沿地表从破土点向两侧蔓延，
        /// 途中灵液气泡从沙面鼓破，越接近破土越密。
        /// </summary>
        private static void UpdateStainSpread(FssStateContext ctx, NPC npc, int t) {
            if (Main.dedServ) {
                return;
            }
            float breachX = npc.Center.X;
            float progress = MathHelper.Clamp(t / (float)FssDirector.IntroStainFrames, 0f, 1f);
            float radius = MathF.Pow(progress, 0.8f) * FssDirector.IntroStainRadius;

            //蔓延前沿：两侧各一撮腐沙 + 碎屑贴地爬
            for (int side = -1; side <= 1; side += 2) {
                float x = breachX + side * radius;
                float groundY = FssVfx.FindGroundY(new Vector2(x, npc.Center.Y - 600f), 900f);
                if (Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustPerfect(new Vector2(x, groundY - 4f), DustID.CorruptGibs,
                        new Vector2(side * Main.rand.NextFloat(0.5f, 1.6f), -Main.rand.NextFloat(0.3f, 1.2f)),
                        90, default, Main.rand.NextFloat(0.8f, 1.2f));
                    d.noGravity = false;
                }
                if (Main.rand.NextBool(3)) {
                    Dust s = Dust.NewDustPerfect(new Vector2(x + Main.rand.NextFloat(-10f, 10f), groundY - 2f),
                        DustID.Sand, new Vector2(side * 0.8f, -Main.rand.NextFloat(0.5f, 1.5f)),
                        110, FssVfx.NecroPlum, Main.rand.NextFloat(0.9f, 1.3f));
                    s.noGravity = false;
                }
            }

            //已污染带内的灵液气泡鼓破（进度越深越密，临破前转急）
            float bubbleChance = progress < 0.85f ? 5f - progress * 3f : 1.4f;
            if (Main.rand.NextBool(Math.Max(1, (int)bubbleChance))) {
                float x = breachX + Main.rand.NextFloat(-radius, radius);
                float groundY = FssVfx.FindGroundY(new Vector2(x, npc.Center.Y - 600f), 900f);
                for (int i = 0; i < 3; i++) {
                    Dust gold = Dust.NewDustPerfect(new Vector2(x, groundY - 2f), DustID.Ichor,
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(1.5f, 3.8f)),
                        30, default, Main.rand.NextFloat(0.8f, 1.2f));
                    gold.noGravity = false;
                }
                if (Main.rand.NextBool(4)) {
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.3f, Pitch = -0.4f, MaxInstances = 3 },
                        new Vector2(x, groundY));
                }
            }
        }

        private static IFssState EndStare(FssStateContext ctx) {
            ctx.AttackCooldown = 24;
            return new FssHubState();
        }
    }
}
