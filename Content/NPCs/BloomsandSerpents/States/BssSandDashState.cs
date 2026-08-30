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
    /// 沙面掠冲（对标克眼假动作冲刺的贴地版）：拉开跑道 → 伏低后撤蓄力（反向运动 +
    /// 尘线车道 + 嘶声）→ 一帧 40px/f 爆冲掠过沙面 → 硬刹 → 连段。
    /// 公平阀：出手前 DashLockLead 帧锁死射向（预告即承诺）；仰角封顶 ±0.24（贴地承诺，
    /// 竖直起跳走破土突袭不走这招）；伤害窗 = 速度门槛；跑道最短 480 杀贴脸秒杀。
    /// P2 起飞行沿途掀沙（慢弧沙弹，非追踪，间隔声明 DashWakeGap）。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.SandDash, typeof(BssStateContext))]
    internal class BssSandDashState : BssStateBase
    {
        public override string StateName => "SandDash";
        public override BssStateIndex StateIndex => BssStateIndex.SandDash;

        private enum DashPhase
        {
            Stalk,   //贴地就位，拉开跑道
            Windup,  //伏低后撤蓄力
            Flight,  //爆冲飞行
            Brake,   //硬刹
        }

        private DashPhase phase;
        private int dashCount;
        /// <summary>锁定射向（锁向帧后不再更新 = 预告即承诺）</summary>
        private Vector2 lockedDir = Vector2.UnitX;
        private float stalkDir = 1f;
        private int wakeTimer;
        /// <summary>掉头助跑累计路程（沿冲刺向的真实位移，链条对齐的计程器）</summary>
        private float alignRun;
        /// <summary>上一帧的冲刺朝向（玩家换边时重置助跑计程）</summary>
        private float lastToward;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phase = DashPhase.Stalk;
            dashCount = 0;
            wakeTimer = 0;
            alignRun = 0f;
            lastToward = 0f;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case DashPhase.Stalk:
                    UpdateStalk(ctx, npc);
                    break;
                case DashPhase.Windup:
                    UpdateWindup(ctx, npc);
                    break;
                case DashPhase.Flight:
                    UpdateFlight(ctx, npc);
                    break;
                case DashPhase.Brake:
                    UpdateBrake(ctx, npc);
                    break;
            }

            //收招：连段打满从硬刹收，超时保险兜底
            bool done = phase == DashPhase.Brake && Timer >= BssDirector.DashBrakeFrames
                && dashCount >= BssDirector.DashReps(ctx.Phase);
            if (done || Counter++ > 60 * 8) {
                npc.velocity *= 0.6f;
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(DashPhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>
        /// 就位两拍：跑道（含助跑余量）不足先贴地退开，随后掉头助跑——沿冲刺线
        /// 前进足够路程把链条重排到身后，之后的后撤蓄力才是"全身拉弓"，
        /// 而不是把脖子甩到冲刺线前方（出手帧穿颈 = 布条感的几何根源）。
        /// </summary>
        private void UpdateStalk(BssStateContext ctx, NPC npc) {
            float dist = Math.Abs(ctx.Target.Center.X - npc.Center.X);
            float toward = FacingToTarget(ctx, 0f);

            //玩家换边：已积的助跑路程作废，重新对齐
            if (lastToward != 0f && Math.Sign(toward) != Math.Sign(lastToward)) {
                alignRun = 0f;
            }
            lastToward = toward;

            //退开段预留助跑要吃掉的余量；一旦开始助跑就不再回头退（防贴脸抖动）
            bool needRetreat = alignRun < 1f
                && dist < BssDirector.DashRunwayMin + BssDirector.DashAlignRunPx;
            stalkDir = needRetreat ? -toward : toward;

            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = stalkDir;
            ctx.CrawlSpeed = BssDirector.CrawlChaseSpeed;
            ctx.LegCommand = BssLegCommand.March;

            //助跑计程：只累计与冲刺向同号的真实位移（掉头减速段不计）
            if (!needRetreat && Math.Sign(npc.velocity.X) == Math.Sign(toward)) {
                alignRun += Math.Abs(npc.velocity.X);
            }

            Timer++;
            bool aligned = alignRun >= BssDirector.DashAlignRunPx;
            if ((Timer >= BssDirector.DashStalkFrames && aligned
                && dist >= BssDirector.DashRunwayMin * 0.8f) || Timer >= 90) {
                SwitchPhase(DashPhase.Windup);
            }
        }

        /// <summary>蓄力：伏低 + 反向后撤（8 次幂迟滞），尘线车道逐渐显形，末段锁向</summary>
        private void UpdateWindup(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            float progress = MathHelper.Clamp(t / (float)BssDirector.DashWindupFrames, 0f, 1f);

            //锁向拍之前追瞄（限制仰角 = 贴地承诺），之后死向
            if (t <= BssDirector.DashWindupFrames - BssDirector.DashLockLead) {
                Vector2 predicted = ctx.Target.Center + ctx.Target.velocity * 12f;
                Vector2 aim = (predicted - npc.Center).SafeNormalize(Vector2.UnitX);
                float ang = MathHelper.Clamp(MathF.Asin(MathHelper.Clamp(aim.Y, -1f, 1f)),
                    -BssDirector.DashMaxPitch, BssDirector.DashMaxPitch);
                float sign = aim.X >= 0f ? 1f : -1f;
                lockedDir = new Vector2(sign * MathF.Cos(ang), MathF.Sin(ang));
            }

            //反向后撤：迟滞收势，最后几帧猛然吸满。链条已对齐在身后，
            //后撤 = 头顶进自己的链（全身后拉），聚拢波把身体向头收拢上膛
            float late = MathF.Pow(progress, 8f);
            ctx.Mode = BssMoveMode.Direct;
            npc.velocity = Vector2.Lerp(npc.velocity, -lockedDir * (3f + 9f * late), 0.25f);
            npc.rotation = npc.rotation.AngleLerp(lockedDir.ToRotation() + BssHead.FacingRot, 0.25f);
            ctx.LegCommand = BssLegCommand.March;
            ctx.Compression = MathHelper.Lerp(1f, 0.86f, progress);
            ctx.GatherLevel = progress;

            if (t == 1 && !Main.dedServ) {
                //蓄力起手音：固定提前量，可被玩家内化（CC0 嘶息素材，低调 = 伏低吸气）
                SoundEngine.PlaySound(SoundID.Item102 with { Volume = 0.75f, Pitch = -0.5f, MaxInstances = 2 }, npc.Center);
            }

            //尘线车道：沿锁向铺出预告线（各端本地，掠冲的"车道预警"）
            if (!Main.dedServ && t > 2) {
                int puffs = 2 + (int)(progress * 3f);
                for (int i = 0; i < puffs; i++) {
                    float along = Main.rand.NextFloat(60f, 240f + 700f * progress);
                    Vector2 pos = npc.Center + lockedDir * along;
                    pos.Y = BssVfx.FindGroundY(pos - new Vector2(0f, 120f)) - 6f;
                    Dust d = Dust.NewDustPerfect(pos, DustID.Sand,
                        new Vector2(lockedDir.X * 1.4f, -Main.rand.NextFloat(0.6f, 1.8f) * (0.5f + progress)),
                        110, default, Main.rand.NextFloat(0.9f, 1.4f) * (0.6f + 0.6f * progress));
                    d.noGravity = Main.rand.NextBool();
                }
                //末段绷紧颤抖
                if (progress > 0.7f) {
                    npc.position += Main.rand.NextVector2Circular(1.5f, 1.5f);
                }
            }

            Timer++;
            if (t >= BssDirector.DashWindupFrames) {
                Launch(ctx, npc);
                SwitchPhase(DashPhase.Flight);
            }
        }

        /// <summary>爆冲：一帧定速 + 沙爆 + 吼声 + 鞭链行波（力量在出手帧）</summary>
        private void Launch(BssStateContext ctx, NPC npc) {
            npc.velocity = lockedDir * BssDirector.DashSpeed;
            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
            }
            ctx.PulseWhip(11f);
            //释放波：蓄力聚拢的长度从头向尾付出去（力从身体流向头）
            ctx.PulseGapWave(SerpentChainMath.WaveRelease, 0.16f);
            if (!Main.dedServ) {
                BssVfx.SandBurst(npc.Center + new Vector2(0f, 16f), 1.4f);
                BssVfx.Roar(npc.Center, -0.35f, 1f);
                BssVfx.Shake(npc.Center, 6f, 1200f);
            }
        }

        /// <summary>飞行：直线承诺不转向，速度门槛开伤害窗，尾迹掀沙（P2 起）</summary>
        private void UpdateFlight(BssStateContext ctx, NPC npc) {
            ctx.Mode = BssMoveMode.Direct;
            ctx.LegCommand = BssLegCommand.Tuck;
            //复利加速：冲刺沿途越冲越快（速度拉伸波随之拉长身体）
            npc.velocity *= 1.012f;
            npc.rotation = npc.velocity.ToRotation() + BssHead.FacingRot;

            float speed = npc.velocity.Length();
            if (speed > BssDirector.DashContactSpeed) {
                npc.damage = npc.defDamage;
            }

            //贴地尘浪（各端本地）
            if (!Main.dedServ && Main.GameUpdateCount % 2 == 0) {
                float groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 160f), 600f);
                if (groundY - npc.Center.Y < 140f) {
                    Dust d = Dust.NewDustPerfect(new Vector2(npc.Center.X, groundY - 4f), DustID.Sand,
                        new Vector2(-npc.velocity.X * 0.12f, -Main.rand.NextFloat(2f, 5f)),
                        90, default, Main.rand.NextFloat(1.1f, 1.7f));
                    d.noGravity = false;
                }
            }

            //尾迹掀沙：P2 起沿路径掀起慢弧沙弹（间隔=DashWakeGap 的声明疏密，非追踪）
            if (ctx.Phase >= 2 && !VaultUtils.isClient && ++wakeTimer >= BssDirector.DashWakeGap) {
                wakeTimer = 0;
                float groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 160f), 600f);
                if (groundY - npc.Center.Y < 160f) {
                    int damage = BssDirector.ScaleProjectileDamage(npc, BssDirector.SandGlobDamage);
                    Vector2 vel = new(-Math.Sign(npc.velocity.X) * Main.rand.NextFloat(0.5f, 2f),
                        -Main.rand.NextFloat(7f, 10f));
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        new Vector2(npc.Center.X, groundY - 8f), vel,
                        ModContent.ProjectileType<BssSandGlob>(), damage, 0.5f, Main.myPlayer);
                }
            }

            Timer++;
            if (Timer >= BssDirector.DashFlightFrames) {
                dashCount++;
                SwitchPhase(DashPhase.Brake);
            }
        }

        /// <summary>硬刹：×0.72/帧的急停沙浪，连段未满回就位</summary>
        private void UpdateBrake(BssStateContext ctx, NPC npc) {
            ctx.Mode = BssMoveMode.Direct;
            ctx.LegCommand = BssLegCommand.March;
            npc.velocity *= 0.66f;
            float speed = npc.velocity.Length();
            if (speed > BssDirector.DashContactSpeed) {
                npc.damage = npc.defDamage;
            }
            if ((int)Timer == 2 && !Main.dedServ) {
                BssVfx.SandBurst(npc.Center + new Vector2(0f, 20f), 0.8f);
            }

            Timer++;
            if (Timer >= BssDirector.DashBrakeFrames && dashCount < BssDirector.DashReps(ctx.Phase)) {
                //下一段连冲重新对齐（冲过头后链条在玩家侧，必须再掉头助跑）
                alignRun = 0f;
                lastToward = 0f;
                SwitchPhase(DashPhase.Stalk);
            }
        }
    }
}
