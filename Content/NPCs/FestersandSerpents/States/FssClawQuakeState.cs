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
    /// 夯地泉列：贴近玩家 → 双杵同举过顶蓄势（比夯地更沉的一记，吼声开场）→
    /// 合砸夯点：震屏 + 灵液冲击柱自夯点向两侧逐座行军喷发（复用
    /// <see cref="FssGeyserColumn"/>：预兆盘 → 冲天泉柱，由近及远的引信行波）。
    /// 公平口径：夯点由编舞函数同源声明；柱距 150px、柱芯判定 ~41px = 站缝即
    /// 逃生道；引信行波可沿波前穿行；每柱自带预兆盘；蛇夯击期钉桩 = 输出白给窗。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.ClawQuake, typeof(FssStateContext))]
    internal class FssClawQuakeState : FssStateBase
    {
        public override string StateName => "ClawQuake";
        public override FssStateIndex StateIndex => FssStateIndex.ClawQuake;

        private enum QuakePhase
        {
            Approach, //贴近出手距离
            Strike,   //单记重砸
            Watch,    //看泉列行军（柱自走）
        }

        private QuakePhase phase;

        public override void OnEnter(FssStateContext ctx) {
            base.OnEnter(ctx);
            phase = QuakePhase.Approach;
        }

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case QuakePhase.Approach:
                    UpdateApproach(ctx, npc);
                    break;
                case QuakePhase.Strike:
                    UpdateStrike(ctx, npc);
                    break;
                case QuakePhase.Watch:
                    //泉列由柱自走，蛇交还爬行压迫（不站桩干等）
                    ctx.Mode = FssMoveMode.Crawl;
                    ctx.CrawlDirX = FacingToTarget(ctx);
                    ctx.CrawlSpeed = 8f;
                    Timer++;
                    if (Timer > FssDirector.QuakeFuseBase
                        + FssDirector.QuakeColumnsPerSide(ctx.Phase) * FssDirector.QuakeFuseStep + 26) {
                        return EndAttack(ctx);
                    }
                    break;
            }

            //超时保险兜底
            if (Counter++ > 60 * 6) {
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(QuakePhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>贴近：爬到出手距离即早退</summary>
        private void UpdateApproach(FssStateContext ctx, NPC npc) {
            float dist = Math.Abs(ctx.Target.Center.X - npc.Center.X);
            ctx.Mode = FssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = FssDirector.CrawlChaseSpeed;
            ctx.LegCommand = FssLegCommand.March;

            Timer++;
            if (dist < FssDirector.SlamRange || Timer >= FssDirector.QuakeApproachFrames) {
                if (!Main.dedServ) {
                    FssVfx.Roar(npc.Center, -0.55f, 1f);
                }
                SwitchPhase(QuakePhase.Strike);
            }
        }

        /// <summary>单记重砸：双杵合砸编舞（同夯地相位映射），砸落帧布下双向泉列</summary>
        private void UpdateStrike(FssStateContext ctx, NPC npc) {
            int t = (int)Timer;

            //钉桩重站架
            ctx.Mode = FssMoveMode.Direct;
            npc.velocity *= 0.85f;
            ctx.LegCommand = FssLegCommand.March;
            ctx.FrontRaise = MathHelper.Clamp(t / (float)FssClawSlamState.ImpactAt * 0.6f, 0f, 0.6f);
            ctx.Compression = Math.Min(ctx.Compression, 0.93f);
            if (ctx.Target.Alives()) {
                float toward = FacingToTarget(ctx, 0f);
                float poseAng = new Vector2(toward, 0.35f).ToRotation();
                npc.rotation = npc.rotation.AngleLerp(poseAng + FssHead.FacingRot, 0.12f);
            }

            ctx.ClawCommand = FssClawCommand.Slam;
            ctx.ClawPhase = MathHelper.Clamp(t / (float)FssDirector.SlamCycleFrames, 0f, 1f);

            //举杵末段亮肿 + 绷紧颤抖（重砸的预告比普通夯地更沉）
            if (t > FssClawSlamState.ImpactAt - 12 && t < FssClawSlamState.ImpactAt) {
                ctx.CystGlow = Math.Max(ctx.CystGlow, 0.85f);
                if (!Main.dedServ) {
                    npc.position += Main.rand.NextVector2Circular(1.3f, 1.3f);
                }
            }

            //砸落帧：夯点同源，布下双向泉列
            if (t == FssClawSlamState.ImpactAt) {
                Vector2 impact = FssClawScript.SlamImpact(npc.Center, npc.rotation, npc.scale);
                ctx.PulseWhip(11f);
                ctx.PulseGapWave(SerpentChainMath.WavePress, 0.14f);
                for (int k = 0; k < ctx.StationBob.Length; k++) {
                    ctx.StationBob[k] = 1.25f;
                }
                if (!Main.dedServ) {
                    FssVfx.CorruptSandBurst(impact, 2.4f);
                    FssVfx.IchorBurst(impact, 2.2f);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.95f, Pitch = -0.6f, MaxInstances = 4 }, impact);
                    FssVfx.Shake(impact, 9f, 1700f);
                }
                if (!VaultUtils.isClient) {
                    PlaceColumns(ctx, npc, impact);
                }
            }

            Timer++;
            if (t >= FssDirector.SlamCycleFrames) {
                SwitchPhase(QuakePhase.Watch);
            }
        }

        /// <summary>
        /// 权威端布泉列：夯点一座即发，两侧各 QuakeColumnsPerSide 座按引信行波
        /// 由近及远喷发（每柱自带预兆盘与柱体演出）；探不到地的落点跳过。
        /// P3 外圈换高柱档。
        /// </summary>
        private static void PlaceColumns(FssStateContext ctx, NPC npc, Vector2 impact) {
            int damage = FssDirector.ScaleProjectileDamage(npc, FssDirector.GeyserDamage);
            int type = ModContent.ProjectileType<FssGeyserColumn>();
            int perSide = FssDirector.QuakeColumnsPerSide(ctx.Phase);

            //夯点本座：最短引信（砸哪喷哪的因果）
            Projectile.NewProjectile(npc.GetSource_FromAI(), impact - new Vector2(0f, 12f),
                Vector2.Zero, type, damage, 0.6f, Main.myPlayer, 8f, 0f);

            for (int k = 1; k <= perSide; k++) {
                int fuse = FssDirector.QuakeFuseBase + k * FssDirector.QuakeFuseStep;
                //P3 外圈高柱档（波尾更凶，逼人贴波前走）
                float tall = ctx.Phase >= 3 && k > perSide - 2 ? 1f : 0f;
                for (int dir = -1; dir <= 1; dir += 2) {
                    float x = impact.X + dir * k * FssDirector.QuakeStepPx;
                    float groundY = FssVfx.FindGroundY(new Vector2(x, impact.Y - 300f), 900f);
                    if (groundY >= impact.Y - 300f + 900f - 1f) {
                        continue; //探不到地（悬崖/深坑）：跳过该座
                    }
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        new Vector2(x, groundY - 12f), Vector2.Zero,
                        type, damage, 0.6f, Main.myPlayer, fuse, tall);
                }
            }
        }
    }
}
