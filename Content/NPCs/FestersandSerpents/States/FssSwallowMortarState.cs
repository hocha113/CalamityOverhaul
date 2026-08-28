using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.States
{
    /// <summary>
    /// A5 吞沙炮（P2 起）：头短暂埋沙吞吸（尘埃向口收束）→ 吞下的沙化成鼓包
    /// 沿体节尾→头加速蠕动（uSwell 行波 = 长身体独有的活体预告，约 70 帧）→
    /// 波至头部昂首喷出巨型腐沙炮弹，玩家上空空爆成下锥霰弹伞 + 金雨。
    /// 公平口径：出手前 MortarLockLead 帧锁向（预告即承诺）；
    /// 空爆伞正下方声明安全眼（缝常量由弹幕发射循环实读）。P3 双弹错拍。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.SwallowMortar, typeof(FssStateContext))]
    internal class FssSwallowMortarState : FssStateBase
    {
        public override string StateName => "SwallowMortar";
        public override FssStateIndex StateIndex => FssStateIndex.SwallowMortar;

        private int TravelEnd => FssDirector.GulpFrames + FssDirector.BulgeTravelFrames;
        private int SecondShellFrame => TravelEnd + FssDirector.MortarSecondDelay;
        private int ExitFrame(int phase) => TravelEnd + FssDirector.MortarRecoverFrames
            + (FssDirector.MortarShells(phase) - 1) * FssDirector.MortarSecondDelay;

        /// <summary>锁定的空爆点（锁向帧钉死）</summary>
        private Vector2 lockedBurst;
        private bool locked;
        private int lastClickCyst;

        public override void OnEnter(FssStateContext ctx) {
            base.OnEnter(ctx);
            locked = false;
            lastClickCyst = -1;
            ctx.RefreshSegments();
        }

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            if (t < FssDirector.GulpFrames) {
                UpdateGulp(ctx, npc, t);
            }
            else if (t < TravelEnd) {
                UpdateBulgeTravel(ctx, npc, t - FssDirector.GulpFrames);
            }
            else if (t == TravelEnd) {
                FireShell(ctx, npc);
            }
            else if (t == SecondShellFrame - 3 && FssDirector.MortarShells(ctx.Phase) >= 2) {
                //第二弹前置提示：升调 + 头闪（错拍弹也要有自己的预告拍）
                ctx.CystGlow = 1f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.7f, Pitch = 0.25f, MaxInstances = 3 }, npc.Center);
                }
            }
            else if (t == SecondShellFrame && FssDirector.MortarShells(ctx.Phase) >= 2) {
                //P3 第二弹：错拍再喷（重新预测，伞缝不变；55 帧飞行即闪避窗）
                locked = false;
                FireShell(ctx, npc);
            }
            else {
                //收势回爬
                ctx.Mode = FssMoveMode.Crawl;
                ctx.CrawlSpeed = 7f;
                ctx.CrawlDirX = FacingToTarget(ctx);
                ctx.LegCommand = FssLegCommand.March;
                if (t > ExitFrame(ctx.Phase)) {
                    return EndAttack(ctx);
                }
            }

            Timer++;
            //超时保险
            if (t > ExitFrame(3) + 40) {
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>埋头吞沙：头压向沙面（Crawl 定桩 + AimAngle 朝下），尘埃向口收束，地面微颤</summary>
        private void UpdateGulp(FssStateContext ctx, NPC npc, int t) {
            ctx.Mode = FssMoveMode.Crawl;
            ctx.CrawlSpeed = 0f;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.LegCommand = FssLegCommand.March;
            //头朝下埋向沙面（Crawl 的 AimAngle 通道负责旋转，不会被速度向覆写）
            ctx.AimAngle = MathHelper.PiOver2;

            float w = t / (float)FssDirector.GulpFrames;
            ctx.SwallowSuction = Math.Max(ctx.SwallowSuction, 0.4f + 0.6f * w);
            ctx.Compression = Math.Min(ctx.Compression, 1f - 0.08f * w);

            if (!Main.dedServ) {
                //尘埃向口收束（吞吸的可见证据）
                Vector2 mouth = npc.Center + new Vector2(0f, 26f);
                for (int i = 0; i < 2; i++) {
                    if (!Main.rand.NextBool(2)) {
                        continue;
                    }
                    Vector2 from = mouth + new Vector2(Main.rand.NextFloat(-110f, 110f), Main.rand.NextFloat(-8f, 6f));
                    Dust d = Dust.NewDustPerfect(from, DustID.Sand,
                        (mouth - from) * 0.09f - new Vector2(0f, 0.4f),
                        100, FssVfx.TaintedSand, Main.rand.NextFloat(0.9f, 1.4f));
                    d.noGravity = true;
                }
                if (t % 14 == 0) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.55f, Pitch = -0.6f + 0.3f * w, MaxInstances = 3 }, npc.Center);
                    FssVfx.Shake(npc.Center, 1.5f + w, 900f);
                }
            }
        }

        /// <summary>鼓包尾→头加速蠕动：长身体独有的活体预告；立姿缓进逼压，末段锁向</summary>
        private void UpdateBulgeTravel(FssStateContext ctx, NPC npc, int local) {
            ctx.Mode = FssMoveMode.Crawl;
            //缓进逼压：预告期身体仍在压上来（钉桩两秒 = 白送输出窗，也丢压迫感）
            ctx.CrawlSpeed = 4.5f;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.LegCommand = FssLegCommand.Raise;

            float w = local / (float)FssDirector.BulgeTravelFrames;
            //加速行波：先慢后快（越近头越急）
            float wave = 1f - w * w;
            ctx.BulgeOrdinal = wave * Math.Max(ctx.TotalSegments, 1);
            ctx.BulgeStrength = 0.55f + 0.45f * w;
            //头随波临近渐抬、渐锁向
            ctx.FrontRaise = MathHelper.Clamp(w * 1.1f, 0f, 1f);
            ctx.CystGlow = Math.Max(ctx.CystGlow, w * 0.8f);

            //锁向（预告即承诺）：末 MortarLockLead 帧钉死空爆点
            if (!locked && local >= FssDirector.BulgeTravelFrames - FssDirector.MortarLockLead) {
                locked = true;
                lockedBurst = PredictTarget(ctx, FssDirector.MortarFlightFrames * 0.5f)
                    - new Vector2(0f, FssDirector.MortarBurstHeight);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.8f, Pitch = 0.1f, MaxInstances = 3 }, npc.Center);
                }
            }
            float aimTo = locked
                ? (lockedBurst - MouthPos(npc)).ToRotation()
                : (ctx.Target.Center - npc.Center).ToRotation() - 0.35f * Math.Sign(ctx.CrawlDirX);
            ctx.AimAngle = aimTo;

            //鼓包扫过囊肿节的咔哒（各端按同步波相同拍）
            int cystIdx = (int)(ctx.BulgeOrdinal / FssDirector.CystStep);
            if (cystIdx != lastClickCyst) {
                lastClickCyst = cystIdx;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.5f, Pitch = -0.3f + 0.6f * w, MaxInstances = 4 }, npc.Center);
                    if (ctx.BulgeOrdinal < ctx.Segments.Count && ctx.Segments.Count > 0) {
                        int ord = (int)MathHelper.Clamp(ctx.BulgeOrdinal, 0f, ctx.Segments.Count - 1);
                        FssVfx.FesterTrickle(ctx.Segments[ord].Center, 1.6f);
                    }
                }
            }
        }

        /// <summary>喷弹帧：巨弹按固定飞时抛向锁定空爆点，全身鞭波后坐</summary>
        private void FireShell(FssStateContext ctx, NPC npc) {
            if (!locked) {
                lockedBurst = PredictTarget(ctx, FssDirector.MortarFlightFrames * 0.5f)
                    - new Vector2(0f, FssDirector.MortarBurstHeight);
                locked = true;
            }
            Vector2 mouth = MouthPos(npc);
            ctx.PulseWhip(13f);
            ctx.BulgeStrength = 0f;
            ctx.BulgeOrdinal = -1f;
            ctx.CystGlow = 1f;

            if (!Main.dedServ) {
                FssVfx.Roar(npc.Center, -0.4f, 1.1f);
                FssVfx.IchorBurst(mouth, 2f, (lockedBurst - mouth).SafeNormalize(-Vector2.UnitY));
                FssVfx.CorruptSandBurst(mouth, 1.2f);
                FssVfx.Shake(npc.Center, 6.5f, 1500f);
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.9f, Pitch = -0.4f, MaxInstances = 3 }, npc.Center);
            }

            if (VaultUtils.isClient) {
                return;
            }
            //固定飞时弹道反解：到点即爆，空爆时刻可预期。
            //不乘弹速爬坡阀：缩速会让实际爆点偏离锁定的承诺点（预告即承诺的机制底线）
            float T = FssDirector.MortarFlightFrames;
            Vector2 delta = lockedBurst - mouth;
            Vector2 vel = new(delta.X / T, delta.Y / T - 0.5f * FssDirector.MortarShellGravity * T);
            int damage = FssDirector.ScaleProjectileDamage(npc, FssDirector.MortarShardDamage);
            Projectile.NewProjectile(npc.GetSource_FromAI(), mouth, vel,
                ModContent.ProjectileType<FssMortarShell>(), damage, 0.6f, Main.myPlayer, T);
        }
    }
}
