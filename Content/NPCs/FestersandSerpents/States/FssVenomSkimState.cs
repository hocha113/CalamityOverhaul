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
    /// A2 掠地毒冲：拉开跑道 → 伏低后撤蓄力（尘线车道预告，出手前 SkimLockLead 帧锁死向）→
    /// 一帧贴地爆冲 → 硬刹 → 连段。冲刺尾迹向后甩灵液滴（雨滴与留池滴交替），
    /// 蛇冲过去、身后跟着落一串金雨——躲完冲刺还要看脚下。
    /// 公平口径：预告即承诺（锁向后不再改向）、接触伤害只在速度 &gt; SkimContactSpeed 时开、
    /// 跑道不足先退开（杀贴脸秒杀）、俯仰钳制贴地掠过。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.VenomSkim, typeof(FssStateContext))]
    internal class FssVenomSkimState : FssStateBase
    {
        public override string StateName => "VenomSkim";
        public override FssStateIndex StateIndex => FssStateIndex.VenomSkim;

        private enum Phase { Stalk, Windup, Flight, Brake }

        private Phase phase;
        private int phaseTimer;
        private Vector2 lockDir;
        private bool locked;
        private int dripIndex;
        /// <summary>掉头助跑累计路程（沿冲刺向的真实位移，链条对齐的计程器）</summary>
        private float alignRun;
        /// <summary>上一帧的冲刺朝向（玩家换边时重置助跑计程）</summary>
        private float lastToward;

        public override void OnEnter(FssStateContext ctx) {
            base.OnEnter(ctx);
            phase = Phase.Stalk;
            phaseTimer = 0;
            locked = false;
            dripIndex = 0;
            alignRun = 0f;
            lastToward = 0f;
        }

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case Phase.Stalk:
                    UpdateStalk(ctx, npc);
                    break;
                case Phase.Windup:
                    UpdateWindup(ctx, npc);
                    break;
                case Phase.Flight:
                    UpdateFlight(ctx, npc);
                    break;
                case Phase.Brake: {
                    IFssState next = UpdateBrake(ctx, npc);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
            }

            phaseTimer++;
            Timer++;

            //超时保险：整招不超过（周期×最大连段+缓冲）
            int repLen = FssDirector.SkimStalkFrames + FssDirector.SkimWindupFrames
                + FssDirector.SkimFlightFrames + FssDirector.SkimBrakeFrames;
            if (Timer > repLen * FssDirector.SkimReps(3) + 80) {
                npc.velocity *= 0.8f;
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>
        /// 就位两拍：跑道（含助跑余量）不足先退开，随后掉头助跑——沿冲刺线前进
        /// 足够路程把链条重排到身后，之后的后撤蓄力才是"全身拉弓"，
        /// 而不是把脖子甩到冲刺线前方（出手帧穿颈 = 布条感的几何根源）。
        /// </summary>
        private void UpdateStalk(FssStateContext ctx, NPC npc) {
            float dx = Math.Abs(ctx.Target.Center.X - npc.Center.X);
            float toward = FacingToTarget(ctx, 0f);

            //玩家换边：已积的助跑路程作废，重新对齐
            if (lastToward != 0f && Math.Sign(toward) != Math.Sign(lastToward)) {
                alignRun = 0f;
            }
            lastToward = toward;

            //退开段预留助跑要吃掉的余量；一旦开始助跑就不再回头退（防贴脸抖动）
            bool needRetreat = alignRun < 1f
                && dx < FssDirector.SkimRunwayMin + FssDirector.SkimAlignRunPx;

            ctx.Mode = FssMoveMode.Crawl;
            ctx.LegCommand = FssLegCommand.March;
            ctx.CrawlDirX = needRetreat ? -toward : toward;
            ctx.CrawlSpeed = FssDirector.CrawlChaseSpeed;

            //助跑计程：只累计与冲刺向同号的真实位移（掉头减速段不计）
            if (!needRetreat && Math.Sign(npc.velocity.X) == Math.Sign(toward)) {
                alignRun += Math.Abs(npc.velocity.X);
            }

            bool aligned = alignRun >= FssDirector.SkimAlignRunPx;
            if ((phaseTimer >= FssDirector.SkimStalkFrames && aligned
                && dx >= FssDirector.SkimRunwayMin * 0.8f) || phaseTimer >= 90) {
                phase = Phase.Windup;
                phaseTimer = 0;
                locked = false;
            }
        }

        /// <summary>蓄力：伏低后撤 + 尘线车道；末 SkimLockLead 帧锁向（预告即承诺）</summary>
        private void UpdateWindup(FssStateContext ctx, NPC npc) {
            ctx.Mode = FssMoveMode.Direct;
            ctx.LegCommand = FssLegCommand.Tuck;
            ctx.Compression = Math.Min(ctx.Compression, 0.9f);

            //锁向前跟踪预测点，锁向后死向
            if (!locked) {
                Vector2 aim = (PredictTarget(ctx, 10f) - npc.Center).SafeNormalize(Vector2.UnitX);
                lockDir = ClampPitch(aim);
                if (phaseTimer >= FssDirector.SkimWindupFrames - FssDirector.SkimLockLead) {
                    locked = true;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.6f, Pitch = 0.5f, MaxInstances = 3 }, npc.Center);
                    }
                }
            }

            //后撤反向运动（二次方迟滞：末段猛吸）；头部快速插值入向（禁一帧瞬转）。
            //链条已对齐在身后，后撤 = 全身后拉，聚拢波把身体向头收拢上膛
            float w = phaseTimer / (float)FssDirector.SkimWindupFrames;
            npc.velocity = -lockDir * (w * w * 9f);
            npc.rotation = npc.rotation.AngleLerp(lockDir.ToRotation() + FssHead.FacingRot, 0.35f);
            ctx.GatherLevel = w;

            //尘线车道（客户端；锁向后线更实）
            if (!Main.dedServ) {
                int per = locked ? 4 : 2;
                for (int i = 0; i < per; i++) {
                    float along = Main.rand.NextFloat(60f, 560f);
                    Dust d = Dust.NewDustPerfect(npc.Center + lockDir * along
                        + lockDir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-12f, 12f),
                        DustID.Sand, lockDir * Main.rand.NextFloat(0.5f, 1.5f),
                        130, FssVfx.TaintedSand, Main.rand.NextFloat(0.9f, 1.3f) * (locked ? 1.2f : 0.9f));
                    d.noGravity = true;
                }
            }

            if (phaseTimer >= FssDirector.SkimWindupFrames) {
                //一帧定初速：力量在出手帧
                npc.velocity = lockDir * FssDirector.SkimSpeed * ctx.RampSpeedScale;
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                ctx.PulseWhip(10f);
                //释放波：蓄力聚拢的长度从头向尾付出去（力从身体流向头）
                ctx.PulseGapWave(SerpentChainMath.WaveRelease, 0.16f);
                if (!Main.dedServ) {
                    FssVfx.Roar(npc.Center, -0.7f, 0.75f);
                    FssVfx.Shake(npc.Center, 4.5f, 1200f);
                    FssVfx.CorruptSandBurst(npc.Bottom, 1.1f);
                }
                phase = Phase.Flight;
                phaseTimer = 0;
            }
        }

        /// <summary>飞行：伤害窗=速度门；尾迹向后甩灵液滴</summary>
        private void UpdateFlight(FssStateContext ctx, NPC npc) {
            ctx.Mode = FssMoveMode.Direct;
            ctx.LegCommand = FssLegCommand.Tuck;
            //复利加速：冲刺沿途越冲越快（速度拉伸波随之拉长身体）
            npc.velocity *= 1.012f;

            //伤害窗与可见冲势同门
            if (npc.velocity.Length() > FssDirector.SkimContactSpeed) {
                npc.damage = npc.defDamage;
            }

            //尾迹滴灵液：留池滴与雨滴交替（池经济播种的机动路径）
            if (!VaultUtils.isClient && phaseTimer % FssDirector.SkimDripGap == 0) {
                dripIndex++;
                int mode = dripIndex % 2 == 0 ? 0 : 1;
                Vector2 from = npc.Center - npc.velocity * Main.rand.NextFloat(0.5f, 2.2f);
                Vector2 vel = -npc.velocity * 0.05f + new Vector2(Main.rand.NextFloat(-1f, 1f), -4.2f);
                int damage = FssDirector.ScaleProjectileDamage(npc, FssDirector.IchorGlobDamage);
                Projectile.NewProjectile(npc.GetSource_FromAI(), from, vel,
                    ModContent.ProjectileType<FssIchorGlob>(), (int)(damage * 0.8f), 0.4f, Main.myPlayer, mode);
            }

            if (phaseTimer >= FssDirector.SkimFlightFrames) {
                phase = Phase.Brake;
                phaseTimer = 0;
            }
        }

        /// <summary>硬刹：×0.66/帧；连段裁决</summary>
        private IFssState UpdateBrake(FssStateContext ctx, NPC npc) {
            ctx.Mode = FssMoveMode.Direct;
            ctx.LegCommand = FssLegCommand.March;
            npc.velocity *= 0.66f;

            if (phaseTimer >= FssDirector.SkimBrakeFrames) {
                Counter++;
                if (Counter >= FssDirector.SkimReps(ctx.Phase) || ctx.Owner.TargetInvalid()) {
                    return EndAttack(ctx);
                }
                //下一段连冲重新对齐（冲过头后链条在玩家侧，必须再掉头助跑）
                alignRun = 0f;
                lastToward = 0f;
                phase = Phase.Stalk;
                phaseTimer = 0;
            }
            return null;
        }

        /// <summary>俯仰钳制：贴地掠冲的身份（射向压在水平 ±SkimMaxPitch 内）</summary>
        private static Vector2 ClampPitch(Vector2 dir) {
            float baseAng = dir.X >= 0f ? 0f : MathHelper.Pi;
            float rel = MathHelper.WrapAngle(dir.ToRotation() - baseAng);
            rel = MathHelper.Clamp(rel, -FssDirector.SkimMaxPitch, FssDirector.SkimMaxPitch);
            return (baseAng + rel).ToRotationVector2();
        }
    }
}
