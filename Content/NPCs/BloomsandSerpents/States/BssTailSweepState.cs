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
    /// 回马甩尾（P3）：故意擦身而过的佯攻冲刺 → 过头急转 U 弯（链条甩成鞭）→
    /// 转弯期红花节沿各自运动向离心甩针 → 转完直接连击掠冲 = 回马枪。
    /// 蛇形身体最自然的变招：每段冲刺仍各自锁向（预告即承诺不破），变化在序列不在单发。
    /// 公平阀声明：甩针方向 = 体节自身位移向（物理离心，非瞄准玩家），针速 10 低于涟漪；
    /// 首冲仰角封顶 ±0.3 贴地承诺；回马枪走掠冲状态自己的蓄力预告，不偷帧。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.TailSweep, typeof(BssStateContext))]
    internal class BssTailSweepState : BssStateBase
    {
        public override string StateName => "TailSweep";
        public override BssStateIndex StateIndex => BssStateIndex.TailSweep;

        private enum SweepPhase
        {
            Stalk,  //拉开擦身跑道
            Windup, //短版后撤蓄力
            Pass,   //擦身冲刺
            Turn,   //过头急转 + 离心甩针
            Settle, //收势接回马枪
        }

        private const int SettleFrames = 8;

        private SweepPhase phase;
        /// <summary>擦身射向（锁向帧后死向）</summary>
        private Vector2 passDir = Vector2.UnitX;
        /// <summary>入弯拉升侧（入弯帧锁定）</summary>
        private Vector2 turnLift;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phase = SweepPhase.Stalk;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case SweepPhase.Stalk:
                    UpdateStalk(ctx, npc);
                    break;
                case SweepPhase.Windup:
                    UpdateWindup(ctx, npc);
                    break;
                case SweepPhase.Pass:
                    UpdatePass(ctx, npc);
                    break;
                case SweepPhase.Turn:
                    UpdateTurn(ctx, npc);
                    break;
                case SweepPhase.Settle:
                    ctx.Mode = BssMoveMode.Direct;
                    ctx.LegCommand = BssLegCommand.Flail;
                    npc.velocity *= 0.85f;
                    npc.rotation = npc.rotation.AngleLerp(
                        (ctx.Target.Center - npc.Center).ToRotation() + BssHead.FacingRot, 0.2f);
                    ctx.BloomGlow = Math.Max(ctx.BloomGlow, 0.8f);
                    Timer++;
                    if (Timer >= SettleFrames) {
                        //回马枪：连击掠冲（其状态自带蓄力预告 = 不偷帧的二段）
                        ctx.QueuedChainState = (int)BssStateIndex.SandDash;
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

        private void SwitchPhase(SweepPhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>就位：跑道不足先退开（可读的拉弓走位）</summary>
        private void UpdateStalk(BssStateContext ctx, NPC npc) {
            float dist = Math.Abs(ctx.Target.Center.X - npc.Center.X);
            float stalkDir = dist < BssDirector.DashRunwayMin
                ? -FacingToTarget(ctx, 0f)
                : FacingToTarget(ctx, 0f);

            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = stalkDir;
            ctx.CrawlSpeed = BssDirector.CrawlChaseSpeed;
            ctx.LegCommand = BssLegCommand.March;

            Timer++;
            if ((Timer >= BssDirector.SweepStalkFrames && dist >= BssDirector.DashRunwayMin * 0.8f)
                || Timer >= BssDirector.SweepStalkFrames * 3) {
                SwitchPhase(SweepPhase.Windup);
            }
        }

        /// <summary>短版蓄力：反向后撤 + 尘线车道，末段锁向（穿过玩家的擦身线）</summary>
        private void UpdateWindup(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            float progress = MathHelper.Clamp(t / (float)BssDirector.SweepWindupFrames, 0f, 1f);

            //锁向拍之前追瞄，仰角封顶 ±0.3（贴地承诺），之后死向
            if (t <= BssDirector.SweepWindupFrames - 4) {
                Vector2 predicted = PredictTarget(ctx, 10f);
                Vector2 aim = (predicted - npc.Center).SafeNormalize(Vector2.UnitX);
                float ang = MathHelper.Clamp(MathF.Asin(MathHelper.Clamp(aim.Y, -1f, 1f)), -0.3f, 0.3f);
                float sign = aim.X >= 0f ? 1f : -1f;
                passDir = new Vector2(sign * MathF.Cos(ang), MathF.Sin(ang));
            }

            ctx.Mode = BssMoveMode.Direct;
            float late = MathF.Pow(progress, 8f);
            npc.velocity = Vector2.Lerp(npc.velocity, -passDir * (3f + 8f * late), 0.25f);
            npc.rotation = npc.rotation.AngleLerp(passDir.ToRotation() + BssHead.FacingRot, 0.25f);
            ctx.LegCommand = BssLegCommand.March;
            ctx.Compression = Math.Min(ctx.Compression, MathHelper.Lerp(1f, 0.88f, progress));

            if (t == 1 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item102 with { Volume = 0.7f, Pitch = -0.4f, MaxInstances = 2 },
                    npc.Center);
            }
            //短版尘线车道（各端本地）
            if (!Main.dedServ && t > 2) {
                for (int i = 0; i < 2; i++) {
                    float along = Main.rand.NextFloat(60f, 160f + 340f * progress);
                    Vector2 pos = npc.Center + passDir * along;
                    pos.Y = BssVfx.FindGroundY(pos - new Vector2(0f, 120f)) - 6f;
                    Dust d = Dust.NewDustPerfect(pos, DustID.Sand,
                        new Vector2(passDir.X * 1.2f, -Main.rand.NextFloat(0.6f, 1.6f)),
                        110, default, Main.rand.NextFloat(0.8f, 1.3f));
                    d.noGravity = Main.rand.NextBool();
                }
            }

            Timer++;
            if (t >= BssDirector.SweepWindupFrames) {
                if (!VaultUtils.isClient) {
                    npc.velocity = passDir * BssDirector.SweepPassSpeed;
                    npc.netUpdate = true;
                }
                ctx.PulseWhip(11f);
                if (!Main.dedServ) {
                    BssVfx.SandBurst(npc.Center + new Vector2(0f, 16f), 1.2f);
                    BssVfx.Roar(npc.Center, -0.3f, 0.9f);
                    BssVfx.Shake(npc.Center, 5f, 1200f);
                }
                SwitchPhase(SweepPhase.Pass);
            }
        }

        /// <summary>擦身冲刺：直线承诺，越过玩家即早退入弯（不磨满时钟）</summary>
        private void UpdatePass(BssStateContext ctx, NPC npc) {
            ctx.Mode = BssMoveMode.Direct;
            ctx.LegCommand = BssLegCommand.Tuck;
            if (npc.velocity.Length() > BssDirector.DashContactSpeed) {
                npc.damage = npc.defDamage;
            }

            float passedBy = Vector2.Dot(npc.Center - ctx.Target.Center, passDir);
            Timer++;
            if (passedBy > BssDirector.SweepOvershoot || Timer >= BssDirector.SweepPassFrames) {
                //入弯：拉升点锁定（先上拔再回瞄，链条甩成鞭）
                turnLift = npc.Center + passDir * 140f + new Vector2(0f, -320f);
                ctx.PulseWhip(14f);
                if (!Main.dedServ) {
                    BssVfx.Roar(npc.Center, 0.1f, 0.85f);
                }
                SwitchPhase(SweepPhase.Turn);
            }
        }

        /// <summary>
        /// 过头急转：两段 Steer（先拉升后回瞄）甩出 U 弯，弯中红花节按各自位移向离心甩针。
        /// 甩针窗口只在入弯前段：针随弯扫成扇，弯后段留给转向收势（针幕有头有尾）。
        /// </summary>
        private void UpdateTurn(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;

            ctx.Mode = BssMoveMode.Steer;
            ctx.MoveTarget = t < 10 ? turnLift : PredictTarget(ctx, 8f);
            ctx.MoveSpeed = 26f;
            ctx.TurnSpeed = 4.6f;
            ctx.AccelRate = 0.18f;
            ctx.LegCommand = BssLegCommand.Flail;

            if (npc.velocity.Length() > 16f) {
                npc.damage = npc.defDamage;
            }

            //离心甩针：按节拍甩，方向 = 体节自身位移向（物理离心，非瞄准）
            if (t < BssDirector.SweepFlingWindow && t % BssDirector.SweepFlingGap == 0) {
                FlingNeedles(ctx, npc);
            }

            Timer++;
            //回瞄到位或满时钟：收势
            Vector2 toPlayer = (ctx.Target.Center - npc.Center).SafeNormalize(Vector2.UnitX);
            float heading = npc.velocity.Length() > 0.5f
                ? MathF.Acos(MathHelper.Clamp(Vector2.Dot(npc.velocity.SafeNormalize(Vector2.UnitX), toPlayer), -1f, 1f))
                : MathHelper.Pi;
            if ((t > 12 && heading < 0.2f) || t >= BssDirector.SweepTurnFrames) {
                SwitchPhase(SweepPhase.Settle);
            }
        }

        /// <summary>红花节离心甩针：只有在动的节才甩（位移 < 6px 的静节跳过 = 甩是鞭出来的）</summary>
        private void FlingNeedles(BssStateContext ctx, NPC npc) {
            if (ctx.Segments.Count == 0) {
                ctx.RefreshSegments();
            }
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.5f, Pitch = 0.3f, MaxInstances = 3 },
                    npc.Center);
            }
            if (VaultUtils.isClient) {
                return;
            }

            int bodyType = ModContent.NPCType<BssBody>();
            int damage = BssDirector.ScaleProjectileDamage(npc, BssDirector.NeedleDamage);
            int type = ModContent.ProjectileType<BssNeedleProj>();
            foreach (var seg in ctx.Segments) {
                if (!seg.Alives() || seg.type != bodyType
                    || !BssStateContext.IsFlowerOrdinal((int)seg.ai[0])) {
                    continue;
                }
                //体节是位置驱动（velocity 恒零），真实运动向取帧间位移
                Vector2 delta = seg.position - seg.oldPosition;
                if (delta.Length() < 6f) {
                    continue;
                }
                Vector2 dir = delta.SafeNormalize(Vector2.UnitX);
                Projectile.NewProjectile(npc.GetSource_FromAI(), seg.Center + dir * 12f,
                    dir * BssDirector.SweepNeedleSpeed, type, damage, 0.4f, Main.myPlayer);
            }
        }
    }
}
