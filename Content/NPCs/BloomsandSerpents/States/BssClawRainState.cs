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
    /// 挥掷沙球雨：退到距离带半锚定 → 前身半立起，左右鳌足交替过顶挥掷——
    /// 每记后引蓄势（慢段 = 预告）→ 3 帧甩出，甩出帧从爪尖（与绘制同一编舞函数取点）
    /// 抖出一梳高弧远程沙球，落点沿横向车道扇散。
    /// 公平口径：车道间距 = 逃生缝声明；高弧慢弹全程可读；贴脸阀低于 260px
    /// 提前收招（邀请骑脸）；半锚定挥掷期 = 远程输出白给窗。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.ClawRain, typeof(BssStateContext))]
    internal class BssClawRainState : BssStateBase
    {
        public override string StateName => "ClawRain";
        public override BssStateIndex StateIndex => BssStateIndex.ClawRain;

        private enum RainPhase
        {
            Position, //退入距离带
            Volley,   //交替挥掷
            Settle,   //收势一拍
        }

        private RainPhase phase;

        private static int FlickCycle => BssDirector.RainFlickWindup
            + BssDirector.RainFlickRelease + BssDirector.RainFlickRecover;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phase = RainPhase.Position;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case RainPhase.Position:
                    UpdatePosition(ctx, npc);
                    break;
                case RainPhase.Volley:
                    UpdateVolley(ctx, npc);
                    break;
                case RainPhase.Settle:
                    ctx.Mode = BssMoveMode.Crawl;
                    ctx.CrawlDirX = FacingToTarget(ctx);
                    ctx.CrawlSpeed = BssDirector.CrawlCruiseSpeed;
                    Timer++;
                    if (Timer > 14) {
                        return EndAttack(ctx);
                    }
                    break;
            }

            //超时保险兜底
            if (Counter++ > 60 * 7) {
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(RainPhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>退入距离带（太近后退、太远逼近，进带即早退）</summary>
        private void UpdatePosition(BssStateContext ctx, NPC npc) {
            float dist = Math.Abs(ctx.Target.Center.X - npc.Center.X);
            float toward = FacingToTarget(ctx, 0f);

            ctx.Mode = BssMoveMode.Crawl;
            ctx.LegCommand = BssLegCommand.March;
            ctx.FrontRaise = MathHelper.Clamp(Timer / 20f, 0f, 0.35f);
            if (dist < BssDirector.RainBandNear) {
                ctx.CrawlDirX = -toward;
                ctx.CrawlSpeed = BssDirector.CrawlChaseSpeed;
            }
            else if (dist > BssDirector.RainBandFar) {
                ctx.CrawlDirX = toward;
                ctx.CrawlSpeed = BssDirector.CrawlChaseSpeed;
            }
            else {
                ctx.CrawlDirX = toward;
                ctx.CrawlSpeed = 3f;
            }

            Timer++;
            bool inBand = dist >= BssDirector.RainBandNear * 0.85f && dist <= BssDirector.RainBandFar * 1.15f;
            if ((Timer > 8 && inBand) || Timer >= BssDirector.RainPositionFrames) {
                SwitchPhase(RainPhase.Volley);
            }
        }

        /// <summary>
        /// 交替挥掷：慢引快甩的抖袖节奏，甩出帧从爪尖抖球。身体半立起原地压桩，
        /// 头盯着玩家（挥掷不追瞄，弹道在甩出帧按预测位解算 = 出手即承诺）。
        /// </summary>
        private void UpdateVolley(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            int cycle = FlickCycle;
            int flickIdx = t / cycle;
            int inFlick = t % cycle;
            int total = BssDirector.RainFlicks(ctx.Phase);
            int side = (flickIdx & 1) == 0 ? 1 : -1;

            //半锚定：贴桩微调向距离带中心
            float dist = Math.Abs(ctx.Target.Center.X - npc.Center.X);
            float bandMid = (BssDirector.RainBandNear + BssDirector.RainBandFar) * 0.5f;
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = dist < bandMid ? -FacingToTarget(ctx, 0f) : FacingToTarget(ctx, 0f);
            ctx.CrawlSpeed = 3.5f;
            ctx.LegCommand = BssLegCommand.March;
            ctx.FrontRaise = 0.55f;
            ctx.Compression = Math.Min(ctx.Compression, 0.95f);
            if (ctx.Target.Alives()) {
                ctx.AimAngle = (ctx.Target.Center - npc.Center).ToRotation();
            }

            //挥掷相位整形：蓄势慢段 0→0.3，甩出快段 0.3→1，收势保持
            float swing01;
            if (inFlick < BssDirector.RainFlickWindup) {
                swing01 = inFlick / (float)BssDirector.RainFlickWindup * 0.3f;
            }
            else if (inFlick < BssDirector.RainFlickWindup + BssDirector.RainFlickRelease) {
                float r = (inFlick - BssDirector.RainFlickWindup) / (float)BssDirector.RainFlickRelease;
                swing01 = 0.3f + r * 0.7f;
            }
            else {
                swing01 = 1f;
            }
            ctx.ClawCommand = BssClawCommand.RainFlick;
            ctx.ClawActiveSide = side;
            ctx.ClawPhase = swing01;

            //蓄势末段亮花（出手预告）
            if (inFlick > BssDirector.RainFlickWindup - 5 && inFlick < BssDirector.RainFlickWindup) {
                ctx.BloomGlow = Math.Max(ctx.BloomGlow, 0.75f);
            }

            //甩出末帧：从爪尖抖球（与绘制同一编舞函数取点 = 出生点咬合）
            if (inFlick == BssDirector.RainFlickWindup + BssDirector.RainFlickRelease - 1
                && flickIdx < total) {
                Vector2 tip = BssClawScript.FlickTip(npc.Center, npc.rotation, side, 0.92f);
                ctx.PulseGapWave(SerpentChainMath.WavePress, 0.06f);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.6f, Pitch = 0.15f, MaxInstances = 3 }, tip);
                    for (int i = 0; i < 5; i++) {
                        Dust d = Dust.NewDustPerfect(tip, DustID.Sand,
                            Main.rand.NextVector2Circular(2.5f, 2.5f) - new Vector2(0f, 2f),
                            100, default, Main.rand.NextFloat(0.9f, 1.3f));
                        d.noGravity = false;
                    }
                }
                if (!VaultUtils.isClient && ctx.Target.Alives()) {
                    ThrowGlobs(ctx, npc, tip);
                }
            }

            Timer++;
            //记满或贴脸阀：收势
            if (flickIdx >= total
                || (ctx.Target.Alives() && dist < BssDirector.RainMinDistance)) {
                SwitchPhase(RainPhase.Settle);
            }
        }

        /// <summary>
        /// 权威端弹道：按预测位与横向车道反解高弧初速（R = v² sin2α / g），
        /// 车道间距 = 落点逃生缝，速度钳制在远程档。
        /// </summary>
        private static void ThrowGlobs(BssStateContext ctx, NPC npc, Vector2 tip) {
            int damage = BssDirector.ScaleProjectileDamage(npc, BssDirector.SandGlobDamage);
            int type = ModContent.ProjectileType<BssSandGlob>();
            Vector2 predicted = PredictTarget(ctx, 18f);
            float hf = Math.Sign(predicted.X - tip.X);
            if (hf == 0f) {
                hf = 1f;
            }
            float baseRange = Math.Abs(predicted.X - tip.X);

            int count = BssDirector.RainGlobsPerFlick;
            for (int k = 0; k < count; k++) {
                float lane = k - (count - 1) * 0.5f;
                float range = Math.Max(baseRange + lane * BssDirector.RainLanePx, 220f);
                float elev = 0.95f - Math.Abs(lane) * 0.08f + Main.rand.NextFloat(-0.05f, 0.05f);
                float v = MathF.Sqrt(range * BssDirector.SandGlobGravity / MathF.Sin(2f * elev));
                v = MathHelper.Clamp(v * Main.rand.NextFloat(0.96f, 1.08f),
                    BssDirector.RainGlobSpeedMin, BssDirector.RainGlobSpeedMax);
                Vector2 vel = new(hf * MathF.Cos(elev) * v, -MathF.Sin(elev) * v);
                Projectile.NewProjectile(npc.GetSource_FromAI(), tip, vel, type, damage, 0.6f, Main.myPlayer);
            }
        }
    }
}
