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
    /// 盘身刺阵（P3 压轴，怒放宣言后的首招）：贴近 → 以玩家原位上方为圆心，头沿巨环起跳
    /// 越过玩家头顶、扎进另一侧沙里、从沙下绕回起点——整条蛇盘成一个半埋的巨环把玩家圈在中间 →
    /// 头定住，露地红花各铺一条黄色预警线指向圈心 → 钉刺沿辐条齐射圈心 →
    /// （P3 第二轮：沿环推进换位再射）→ 解环爬走。
    /// 蛇的盘身本能第一次成为招式：环本身是 1.5 秒的可见预告，辐条把"别站哪"画在地上。
    /// 公平阀声明：圆心在起环帧锁定不追人，环半径 RingRadius 远大于玩家身位（圈内近心带不被
    /// 环身扫到）；环身接触伤只在运动中生效（定住的环可以直接走出去）；辐条 RingSpokeTelegraph 帧
    /// 预警 + 锁定；钉刺近乎一线，站离辐条与圈心即安全；埋沙的红花不发射。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.CoilRing, typeof(BssStateContext))]
    internal class BssCoilRingState : BssStateBase
    {
        public override string StateName => "CoilRing";
        public override BssStateIndex StateIndex => BssStateIndex.CoilRing;

        private enum RingPhase
        {
            Approach, //贴近
            Encircle, //沿环合围
            Spoke,    //辐条预警 + 齐射
            Shift,    //沿环推进换位
            Unwind,   //解环
        }

        private RingPhase phase;
        /// <summary>圆心（起环帧锁定）</summary>
        private Vector2 center;
        /// <summary>当前环角（弧度）与旋向</summary>
        private float angle;
        private float spinDir = 1f;
        /// <summary>本段已扫过的环角</summary>
        private float swept;
        private int volleys;
        private bool fired;
        private float prevY;
        /// <summary>穿面沙爆的去抖（上一帧是否在地下）</summary>
        private bool wasUnderground;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phase = RingPhase.Approach;
            volleys = 0;
            fired = false;
            swept = 0f;
            prevY = ctx.Npc.Center.Y;
            ctx.RefreshSegments();
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case RingPhase.Approach:
                    UpdateApproach(ctx, npc);
                    break;
                case RingPhase.Encircle:
                    UpdateEncircle(ctx, npc);
                    break;
                case RingPhase.Spoke:
                    UpdateSpoke(ctx, npc);
                    break;
                case RingPhase.Shift:
                    UpdateShift(ctx, npc);
                    break;
                case RingPhase.Unwind: {
                    IBssState next = UpdateUnwind(ctx, npc);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
            }
            prevY = npc.Center.Y;

            //超时保险兜底
            if (Counter++ > 60 * 9) {
                npc.velocity *= 0.5f;
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(RingPhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>贴近：爬到起环距离即锁圆心起环</summary>
        private void UpdateApproach(BssStateContext ctx, NPC npc) {
            float dx = ctx.Target.Center.X - npc.Center.X;
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = BssDirector.CrawlChaseSpeed;
            ctx.LegCommand = BssLegCommand.March;

            Timer++;
            if (Math.Abs(dx) < 520f || Timer >= BssDirector.RingApproachFrames) {
                float g = BssVfx.FindGroundY(new Vector2(ctx.Target.Center.X, ctx.Target.Center.Y - 300f), 1200f);
                center = new Vector2(ctx.Target.Center.X, g - BssDirector.RingCenterLift);
                Vector2 rel = npc.Center - center;
                angle = rel.ToRotation();
                //先向上越顶：头在圆心左侧则角增，右侧则角减（屏幕系 y 向下）
                spinDir = rel.X <= 0f ? 1f : -1f;
                swept = 0f;
                wasUnderground = false;
                ctx.PulseWhip(9f);
                if (!Main.dedServ) {
                    BssVfx.Roar(npc.Center, -0.3f, 1f);
                    BssVfx.SandBurst(npc.Bottom, 1.4f);
                    BssVfx.Shake(npc.Center, 6f, 1300f);
                }
                SwitchPhase(RingPhase.Encircle);
            }
        }

        /// <summary>沿环推进一帧：前 RingMergeFrames 帧并轨（钳制单帧位移），之后精确沿环</summary>
        private void AdvanceAlongRing(BssStateContext ctx, NPC npc, bool merging) {
            float omega = BssDirector.RingHeadSpeed / BssDirector.RingRadius;
            angle += omega * spinDir;
            swept += omega;
            Vector2 desired = center + angle.ToRotationVector2() * BssDirector.RingRadius;
            Vector2 step = desired - npc.Center;
            if (merging && step.Length() > 30f) {
                step = step.SafeNormalize(Vector2.Zero) * 30f;
            }
            ctx.Mode = BssMoveMode.Direct;
            npc.velocity = step;
            ctx.LegCommand = BssLegCommand.Flail;
            ctx.ClawCommand = BssClawCommand.Tuck;
            DeclareJaw(ctx, BssJawCommand.Gape);

            //环身运动中接触伤开（伤害窗 = 可见冲势）
            if (step.Length() > BssDirector.DashContactSpeed * 0.7f) {
                npc.damage = npc.defDamage;
            }
            UpdateCrossFx(ctx, npc);
        }

        /// <summary>合围：扫满一整圈即成环</summary>
        private void UpdateEncircle(BssStateContext ctx, NPC npc) {
            AdvanceAlongRing(ctx, npc, Timer < BssDirector.RingMergeFrames);
            ctx.Compression = 1f;

            //环身高速沙沫：头沿环飞掠时带沙（各端本地）
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(24f, 24f), DustID.Sand,
                    -npc.velocity * 0.12f, 110, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = true;
            }

            Timer++;
            if (swept >= MathHelper.TwoPi) {
                fired = false;
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                SwitchPhase(RingPhase.Spoke);
            }
        }

        /// <summary>
        /// 辐条：头定住（环静止 = 环身无伤），起手帧每朵露地红花铺一条指向圈心的黄线，
        /// 全花预亮 + 逐节咔嗒；预警走完沿辐条齐射钉刺。
        /// </summary>
        private void UpdateSpoke(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            ctx.Mode = BssMoveMode.Hold;
            npc.velocity *= 0.6f;
            //定住时腿回步行档：够得着地就踩住，够不着自动腾空卷曲
            ctx.LegCommand = BssLegCommand.March;
            ctx.ClawCommand = BssClawCommand.Brace;
            ctx.ClawPhase = MathHelper.Clamp(t / 10f, 0f, 1f);
            ctx.PulseKind = 4;
            ctx.BloomGlow = Math.Max(ctx.BloomGlow, 0.85f);
            //头看圈心
            npc.rotation = npc.rotation.AngleLerp((center - npc.Center).ToRotation() + BssHead.FacingRot, 0.15f);

            if (t < BssDirector.RingSpokeTelegraph) {
                DeclareJaw(ctx, BssJawCommand.Inhale, t / (float)BssDirector.RingSpokeTelegraph);
            }
            else {
                DeclareJaw(ctx, BssJawCommand.Spit);
            }

            if (t == 0) {
                LaySpokes(ctx, npc);
            }
            if (!fired && t >= BssDirector.RingSpokeTelegraph) {
                fired = true;
                FireSpokes(ctx, npc);
            }

            Timer++;
            if (t >= BssDirector.RingSpokeTelegraph + 12) {
                volleys++;
                if (volleys < BssDirector.RingVolleys(ctx.Phase)) {
                    swept = 0f;
                    SwitchPhase(RingPhase.Shift);
                }
                else {
                    SwitchPhase(RingPhase.Unwind);
                }
            }
        }

        /// <summary>沿环推进换位：红花转到新方位，再铺一轮辐条</summary>
        private void UpdateShift(BssStateContext ctx, NPC npc) {
            AdvanceAlongRing(ctx, npc, Timer < 6);
            Timer++;
            if (Timer >= BssDirector.RingShiftFrames) {
                fired = false;
                SwitchPhase(RingPhase.Spoke);
            }
        }

        /// <summary>解环：头奔玩家远侧地表爬走，链自然解开</summary>
        private IBssState UpdateUnwind(BssStateContext ctx, NPC npc) {
            ctx.LegCommand = BssLegCommand.Tuck;
            DeclareJaw(ctx, BssJawCommand.Clamp);
            ctx.Mode = BssMoveMode.Steer;
            float side = Math.Sign(npc.Center.X - center.X);
            if (side == 0f) {
                side = 1f;
            }
            float exitX = center.X + side * (BssDirector.RingRadius + 260f);
            float exitGround = BssVfx.FindGroundY(new Vector2(exitX, center.Y - 300f), 1200f);
            ctx.MoveTarget = new Vector2(exitX, exitGround - BssDirector.CrawlRideHeight);
            ctx.MoveSpeed = 18f;
            ctx.TurnSpeed = 2.8f;
            ctx.AccelRate = 0.1f;
            UpdateCrossFx(ctx, npc);

            Timer++;
            bool arrived = Vector2.Distance(npc.Center, ctx.MoveTarget) < 90f;
            if (arrived || Timer >= BssDirector.RingUnwindFrames) {
                npc.velocity *= 0.5f;
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>铺辐条：每朵露地红花一条定向预警线指向圈心（权威端），各端咔嗒 + 花光</summary>
        private void LaySpokes(BssStateContext ctx, NPC npc) {
            ctx.RefreshSegments();
            ctx.PulseWhip(5f);
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item102 with { Volume = 0.7f, Pitch = -0.1f, MaxInstances = 3 }, center);
                BssVfx.Roar(npc.Center, 0f, 0.7f);
            }
            if (VaultUtils.isClient) {
                return;
            }
            int bodyType = ModContent.NPCType<BssBody>();
            int omenType = ModContent.ProjectileType<BssDashOmen>();
            foreach (var seg in ctx.Segments) {
                if (!seg.Alives() || seg.type != bodyType) {
                    continue;
                }
                if (!BssStateContext.IsFlowerOrdinal((int)seg.ai[0]) || !BssVfx.IsAboveGround(seg.Center)) {
                    continue;
                }
                Vector2 dir = (center - seg.Center).SafeNormalize(Vector2.UnitY);
                Projectile.NewProjectile(npc.GetSource_FromAI(), seg.Center, dir, omenType, 0, 0f, Main.myPlayer,
                    -1f, -1f, BssDashOmen.PackParams(0, BssDirector.RingSpokeTelegraph, BssDirector.RingSpokeLock));
            }
        }

        /// <summary>齐射：每朵露地红花沿辐条向圈心射 RingNeedlesPerSpoke 枚钉刺（近乎一线）</summary>
        private void FireSpokes(BssStateContext ctx, NPC npc) {
            ctx.JawBurst = 1f;
            ctx.ShakeStrength = 0.8f;
            ctx.PulseWhip(7f);
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.8f, Pitch = 0.15f, MaxInstances = 3 }, center);
                BssVfx.Shake(center, 4f, 1400f);
            }
            int bodyType = ModContent.NPCType<BssBody>();
            int damage = BssDirector.ScaleProjectileDamage(npc, BssDirector.NeedleDamage);
            int type = ModContent.ProjectileType<BssNeedleProj>();
            foreach (var seg in ctx.Segments) {
                if (!seg.Alives() || seg.type != bodyType) {
                    continue;
                }
                if (!BssStateContext.IsFlowerOrdinal((int)seg.ai[0]) || !BssVfx.IsAboveGround(seg.Center)) {
                    continue;
                }
                Vector2 dir = (center - seg.Center).SafeNormalize(Vector2.UnitY);
                if (!Main.dedServ) {
                    for (int i = 0; i < 4; i++) {
                        Dust d = Dust.NewDustPerfect(seg.Center + dir * (12f * seg.scale), DustID.JunglePlants,
                            dir.RotatedByRandom(0.4f) * Main.rand.NextFloat(1.5f, 3.5f), 100, default, 1f);
                        d.noGravity = true;
                    }
                }
                if (VaultUtils.isClient) {
                    continue;
                }
                int n = BssDirector.RingNeedlesPerSpoke;
                for (int i = 0; i < n; i++) {
                    float spread = MathHelper.Lerp(-BssDirector.RingSpokeSpread, BssDirector.RingSpokeSpread, i / (float)(n - 1));
                    //同线错速：一线上前后排开，读作一串而不是一枚
                    float speed = BssDirector.NeedleSpeed * (0.85f + 0.1f * i);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), seg.Center + dir * (14f * seg.scale),
                        dir.RotatedBy(spread) * speed, type, damage, 0.5f, Main.myPlayer);
                }
            }
            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
            }
        }

        /// <summary>穿面沙爆（各端本地）：头沿环扎进沙里、从沙里钻出各一记</summary>
        private void UpdateCrossFx(BssStateContext ctx, NPC npc) {
            float g = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 400f), 1200f);
            bool underground = npc.Center.Y > g + 10f;
            if (underground == wasUnderground) {
                return;
            }
            wasUnderground = underground;
            ctx.PulseWhip(underground ? 8f : 10f);
            if (Main.dedServ) {
                return;
            }
            BssVfx.SandBurst(new Vector2(npc.Center.X, g), underground ? 1.3f : 1.6f);
            BssVfx.Shake(npc.Center, underground ? 4f : 6f);
            if (!underground) {
                BssVfx.Roar(npc.Center, -0.4f, 0.8f);
            }
        }
    }
}
