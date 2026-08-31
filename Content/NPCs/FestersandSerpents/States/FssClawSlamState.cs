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
    /// 疮杵夯地：贴近玩家 → 双杵同举过顶蓄势（慢段 = 预告）→ 合砸夯点：震屏 +
    /// 重团直落 + **灵液喷泉爆发**——高抛灵液团自夯点炸起（弧顶 400~500px），
    /// 落点两翼扩散播种脓池（给满场引爆喂燃料的前菜），2~3 记连夯。
    /// 公平口径：夯点由编舞函数同源声明（举杵方向即夯向）；喷泉重力慢弧全程可读，
    /// 中央近竖直两翼渐远 = 落点扇声明；蛇夯击期钉桩 = 输出白给窗。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.ClawSlam, typeof(FssStateContext))]
    internal class FssClawSlamState : FssStateBase
    {
        public override string StateName => "ClawSlam";
        public override FssStateIndex StateIndex => FssStateIndex.ClawSlam;

        private enum SlamPhase
        {
            Approach, //贴近出手距离
            Strike,   //连记夯击
            Settle,   //收势一拍
        }

        private SlamPhase phase;
        /// <summary>已完成的夯击记数</summary>
        private int reps;

        /// <summary>砸落发生在编舞 0.62 处</summary>
        internal static int ImpactAt => (int)(FssDirector.SlamCycleFrames * 0.62f);

        public override void OnEnter(FssStateContext ctx) {
            base.OnEnter(ctx);
            phase = SlamPhase.Approach;
            reps = 0;
        }

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case SlamPhase.Approach:
                    UpdateApproach(ctx, npc);
                    break;
                case SlamPhase.Strike:
                    UpdateStrike(ctx, npc);
                    break;
                case SlamPhase.Settle:
                    ctx.Mode = FssMoveMode.Crawl;
                    ctx.CrawlDirX = FacingToTarget(ctx);
                    ctx.CrawlSpeed = FssDirector.CrawlCruiseSpeed;
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

        private void SwitchPhase(SlamPhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>贴近：爬到出手距离即早退（不磨蹭）</summary>
        private void UpdateApproach(FssStateContext ctx, NPC npc) {
            float dist = Math.Abs(ctx.Target.Center.X - npc.Center.X);
            ctx.Mode = FssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = FssDirector.CrawlChaseSpeed;
            ctx.LegCommand = FssLegCommand.March;

            Timer++;
            if (dist < FssDirector.SlamRange || Timer >= FssDirector.SlamApproachFrames) {
                SwitchPhase(SlamPhase.Strike);
            }
        }

        /// <summary>
        /// 连记夯击：钉桩面向玩家，双杵走合砸编舞；砸落帧震屏 + 重团直落 +
        /// 高抛灵液喷泉自夯点炸起（弧顶 400~500px，落点两翼扩散播池）。
        /// </summary>
        private void UpdateStrike(FssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            int cycleT = t % FssDirector.SlamCycleFrames;

            //钉桩重站架：缓刹 + 面向玩家 + 前身半立
            ctx.Mode = FssMoveMode.Direct;
            npc.velocity *= 0.85f;
            ctx.LegCommand = FssLegCommand.March;
            ctx.FrontRaise = MathHelper.Clamp(0.3f + 0.3f * MathF.Sin(cycleT / (float)FssDirector.SlamCycleFrames * MathHelper.Pi), 0f, 0.6f);
            ctx.Compression = Math.Min(ctx.Compression, 0.95f);
            if (ctx.Target.Alives()) {
                float toward = FacingToTarget(ctx, 0f);
                float poseAng = new Vector2(toward, 0.35f).ToRotation();
                npc.rotation = npc.rotation.AngleLerp(poseAng + FssHead.FacingRot, 0.12f);
            }

            //鳌足夯地编舞（各端同相位）
            ctx.ClawCommand = FssClawCommand.Slam;
            ctx.ClawPhase = cycleT / (float)FssDirector.SlamCycleFrames;

            //举杵末段亮肿（出手预告）
            if (cycleT > ImpactAt - 10 && cycleT < ImpactAt) {
                ctx.CystGlow = Math.Max(ctx.CystGlow, 0.7f);
            }

            //砸落帧：夯点从编舞函数同源取得（与双杵绘制的落点必然一致）
            if (cycleT == ImpactAt) {
                Vector2 impact = FssClawScript.SlamImpact(npc.Center, npc.rotation, npc.scale);
                FireEruption(ctx, npc, impact);
            }

            Timer++;
            //一记走完：记满收势，未满接下一记（编舞自然循环）
            if (cycleT == FssDirector.SlamCycleFrames - 1) {
                reps++;
                if (reps >= FssDirector.SlamReps(ctx.Phase)) {
                    SwitchPhase(SlamPhase.Settle);
                }
            }
        }

        /// <summary>
        /// 夯击兑现：震屏 + 冲击尘 + 重团直落砸大池 + 高抛喷泉扇（中央近竖直、
        /// 两翼渐远的重力慢弧，落点 100~700px 扩散播池）。
        /// </summary>
        private static void FireEruption(FssStateContext ctx, NPC npc, Vector2 impact) {
            ctx.PulseWhip(10f);
            ctx.PulseGapWave(SerpentChainMath.WavePress, 0.12f);
            for (int k = 0; k < ctx.StationBob.Length; k++) {
                ctx.StationBob[k] = 1.2f;
            }
            if (!Main.dedServ) {
                FssVfx.CorruptSandBurst(impact, 2.2f);
                FssVfx.IchorBurst(impact, 2f);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.5f, MaxInstances = 4 }, impact);
                SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.6f, Pitch = -0.25f, MaxInstances = 4 }, impact);
                FssVfx.Shake(impact, 8f, 1600f);
            }

            if (VaultUtils.isClient) {
                return;
            }
            int globDamage = FssDirector.ScaleProjectileDamage(npc, FssDirector.IchorGlobDamage);
            int type = ModContent.ProjectileType<FssIchorGlob>();
            //夯点重团直落：原地砸出大池
            Projectile.NewProjectile(npc.GetSource_FromAI(), impact - new Vector2(0f, 26f),
                new Vector2(0f, 3f), type, globDamage, 0.7f, Main.myPlayer, 2f);

            //高抛喷泉扇：自竖直向两翼展开——中央满速高柱（弧顶 ~500px），
            //两翼只降到八成速（角度放平 + 速度仍足 = 远抛 600~1000px 落点播池）
            int count = FssDirector.SlamEruptGlobs;
            for (int i = 0; i < count; i++) {
                float lane = count > 1 ? i / (float)(count - 1) * 2f - 1f : 0f;
                float ang = -MathHelper.PiOver2 + lane * FssDirector.SlamEruptHalfArc
                    + Main.rand.NextFloat(-0.05f, 0.05f);
                float speed = MathHelper.Lerp(FssDirector.SlamEruptSpeedMax, FssDirector.SlamEruptSpeedMin,
                    Math.Abs(lane) * 0.55f) * Main.rand.NextFloat(0.94f, 1.08f) * ctx.RampSpeedScale;
                Projectile.NewProjectile(npc.GetSource_FromAI(), impact - new Vector2(0f, 10f),
                    ang.ToRotationVector2() * speed, type, globDamage, 0.6f, Main.myPlayer);
            }
        }
    }
}
