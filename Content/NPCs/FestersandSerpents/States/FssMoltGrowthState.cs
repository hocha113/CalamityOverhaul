using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.States
{
    /// <summary>
    /// P2 转阶段「蜕变生长」（62% 血线，全程无伤）：清弹 → 盘落钻地 → 破土冲天 →
    /// 空中抽搐、蜕皮波头→尾撕裂（旧皮壳屑甩离）→ 裂缝处当场长出 +GrowthSegments 节
    /// （出生胀大 + 逐节金闪）→ 重落地震屏，腐沙暴全开。
    /// 「上位变异」的实体化时刻：这条虫在玩家眼前变得更长更大。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.MoltGrowth, typeof(FssStateContext))]
    internal class FssMoltGrowthState : FssStateBase
    {
        public override string StateName => "MoltGrowth";
        public override FssStateIndex StateIndex => FssStateIndex.MoltGrowth;

        private const int CoilEnd = 34;
        private const int DiveEnd = 78;
        /// <summary>破土冲天帧</summary>
        private const int LaunchFrame = 96;
        /// <summary>抽搐蜕皮段（腾空滞态）</summary>
        private const int ConvulseFrames = 88;
        private const int ConvulseEnd = LaunchFrame + ConvulseFrames;
        /// <summary>生长帧（抽搐中段，蜕皮波扫过一半时）</summary>
        private const int GrowFrame = LaunchFrame + 44;
        private const int Timeout = 330;

        private bool grewDone;
        private bool breachFxDone;
        private float prevY;
        private int lastHuskOrdinal = -1;

        public override void OnEnter(FssStateContext ctx) {
            base.OnEnter(ctx);
            grewDone = false;
            breachFxDone = false;
            prevY = ctx.Npc.Center.Y;
            lastHuskOrdinal = -1;
            ctx.RefreshSegments();
            FssVfx.ClearOwnHostileProjectiles();
        }

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            if (t < CoilEnd) {
                //盘落收势：贴地减速，腿收拢
                ctx.Mode = FssMoveMode.Direct;
                npc.velocity *= 0.88f;
                ctx.LegCommand = FssLegCommand.Tuck;
                ctx.LegAlpha = MathHelper.Clamp(1f - t / 20f, 0f, 1f);
                ctx.Compression = Math.Min(ctx.Compression, 0.9f);
                if (t % 10 == 0 && !Main.dedServ) {
                    FssVfx.FesterTrickle(npc.Center, 1.5f);
                }
            }
            else if (t < DiveEnd) {
                //钻地下潜
                ctx.Mode = FssMoveMode.Direct;
                ctx.LegCommand = FssLegCommand.Tuck;
                ctx.LegAlpha = 0f;
                npc.velocity.X *= 0.97f;
                npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y + 0.8f, -10f, 26f);
                DiveFaceFX(ctx, npc);
            }
            else if (t < LaunchFrame) {
                //地下就位：钉在玩家侧下方蓄势
                ctx.Mode = FssMoveMode.Hold;
                npc.velocity = Vector2.Zero;
                if (!VaultUtils.isClient && ctx.Target.Alives() && t == DiveEnd) {
                    float groundY = FssVfx.FindGroundY(ctx.Target.Center - new Vector2(0f, 200f));
                    npc.Center = new Vector2(ctx.Target.Center.X + 260f * Math.Sign(npc.Center.X - ctx.Target.Center.X + 0.1f),
                        groundY + 420f);
                    npc.netUpdate = true;
                }
            }
            else if (t == LaunchFrame) {
                //破土冲天：一帧定初速
                if (!VaultUtils.isClient) {
                    npc.velocity = new Vector2(0f, -34f);
                    npc.netUpdate = true;
                }
                ctx.Mode = FssMoveMode.Direct;
            }
            else if (t < ConvulseEnd) {
                //腾空抽搐蜕皮：升势渐缓悬滞，全身剧颤，蜕皮波头→尾撕过
                ctx.Mode = FssMoveMode.Direct;
                ctx.LegCommand = FssLegCommand.Flail;
                ctx.LegAlpha = 0.85f;
                npc.velocity *= 0.94f;
                npc.velocity.Y -= 0.12f; //缓浮：抽搐期悬在空中

                float w = (t - LaunchFrame) / (float)ConvulseFrames;
                ctx.ShakeStrength = Math.Max(ctx.ShakeStrength, 0.85f);
                ctx.PulseKind = 2;
                ctx.PulsePhase = w;
                ctx.ErodeLevel = Math.Max(ctx.ErodeLevel, MathF.Sin(w * MathHelper.Pi) * 0.6f);

                //蜕皮波前：体节撕裂 FX（本地）+ 旧壳屑甩离（权威端，每 MoltHuskEvery 节一片）
                if (ctx.TotalSegments > 0) {
                    int ord = (int)(w * ctx.TotalSegments);
                    if (ord != lastHuskOrdinal && ord < ctx.Segments.Count && ctx.Segments[ord].active) {
                        lastHuskOrdinal = ord;
                        NPC seg = ctx.Segments[ord];
                        if (!Main.dedServ) {
                            FssVfx.IchorBurst(seg.Center, 0.8f, Main.rand.NextVector2Unit());
                            Dust rip = Dust.NewDustPerfect(seg.Center, DustID.CorruptGibs,
                                Main.rand.NextVector2Circular(4f, 4f) - new Vector2(0f, 2f),
                                60, default, Main.rand.NextFloat(1.1f, 1.6f));
                            rip.noGravity = false;
                        }
                        if (!VaultUtils.isClient && ord % FssDirector.MoltHuskEvery == 0) {
                            float chainDir = seg.rotation - FssHead.FacingRot;
                            float flank = ord % (FssDirector.MoltHuskEvery * 2) == 0 ? 1f : -1f;
                            Vector2 vel = (chainDir + MathHelper.PiOver2 * flank).ToRotationVector2()
                                * FssDirector.MoltHuskSpeed + new Vector2(0f, -2.5f);
                            int damage = FssDirector.ScaleProjectileDamage(npc, FssDirector.HuskDamage);
                            Projectile.NewProjectile(npc.GetSource_FromAI(), seg.Center, vel,
                                Terraria.ModLoader.ModContent.ProjectileType<Projectiles.FssHuskShard>(),
                                damage, 0.4f, Main.myPlayer);
                        }
                    }
                }
                if (t % 12 == 0 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.6f, Pitch = -0.4f, MaxInstances = 3 }, npc.Center);
                    FssVfx.Shake(npc.Center, 2.6f, 1300f);
                }

                //生长帧：裂缝处当场长节（权威端），全端可见出生胀大
                if (t == GrowFrame) {
                    ctx.Phase = 2;
                    if (!VaultUtils.isClient) {
                        FssHead.GrowBodySegments(npc, FssDirector.GrowthSegments);
                    }
                    ctx.RefreshSegments();
                    if (!Main.dedServ) {
                        FssVfx.Roar(npc.Center, -0.7f, 1.2f);
                        FssVfx.IchorBurst(npc.Center, 2f);
                        FssVfx.Shake(npc.Center, 7f, 1500f);
                    }
                    ctx.PulseWhip(14f);
                }
            }
            else {
                //坠落回场：重落地收束
                ctx.Mode = FssMoveMode.Direct;
                ctx.LegCommand = FssLegCommand.Flail;
                ctx.LegAlpha = 1f;
                npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y + 0.65f, -30f, 22f);
                npc.velocity.X *= 0.99f;

                float groundY = FssVfx.FindGroundY(npc.Center - new Vector2(0f, 60f));
                if (npc.velocity.Y > 0f && npc.Center.Y >= groundY - FssDirector.CrawlRideHeight) {
                    if (!Main.dedServ) {
                        FssVfx.CorruptSandBurst(new Vector2(npc.Center.X, groundY), 2f);
                        FssVfx.IchorBurst(npc.Center, 1.4f);
                        FssVfx.Shake(npc.Center, 8f, 1600f);
                    }
                    ctx.PulseWhip(11f);
                    return Finish(ctx);
                }
            }

            prevY = npc.Center.Y;
            Timer++;

            //超时保险
            if (t > Timeout) {
                return Finish(ctx);
            }
            return null;
        }

        /// <summary>入土穿面表现（本地位置检测）</summary>
        private void DiveFaceFX(FssStateContext ctx, NPC npc) {
            if (Main.dedServ || breachFxDone) {
                return;
            }
            float groundY = FssVfx.FindGroundY(npc.Center - new Vector2(0f, 300f), 900f);
            if (prevY < groundY && npc.Center.Y >= groundY - 10f) {
                breachFxDone = true;
                FssVfx.CorruptSandBurst(new Vector2(npc.Center.X, groundY), 1.4f);
            }
        }

        private IFssState Finish(FssStateContext ctx) {
            if (ctx.Phase < 2) {
                ctx.Phase = 2;
                if (!VaultUtils.isClient && !grewDone) {
                    FssHead.GrowBodySegments(ctx.Npc, FssDirector.GrowthSegments);
                }
            }
            grewDone = true;
            ctx.PostTransitionRamp = FssDirector.PostTransitionRampFrames;
            ctx.AttackCooldown = 30;
            return new FssHubState();
        }
    }
}
