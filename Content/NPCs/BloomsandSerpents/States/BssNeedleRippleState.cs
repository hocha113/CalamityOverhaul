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
    /// 针刺涟漪（P2 起）：前半身拱起 → 预告波沿体节亮过红花 → 静默一拍 →
    /// 发射波扫过红花节时朝体外法向射钉刺，涟漪推进不齐射。
    /// 公平阀声明：只有红花节发射（FlowerStep=3 的空间缺口）、钉刺出手即死向不追踪、
    /// 埋沙的节不发射；拱身持位 = 阵型相对稳定。P3 追加尾→头反向第二波。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.NeedleRipple, typeof(BssStateContext))]
    internal class BssNeedleRippleState : BssStateBase
    {
        public override string StateName => "NeedleRipple";
        public override BssStateIndex StateIndex => BssStateIndex.NeedleRipple;

        private const int ArchFrames = 16;
        private const int QuietFrames = 6;
        private int TelegraphEnd => ArchFrames + BssDirector.RippleTelegraphFrames;
        private int FireFrom => TelegraphEnd + QuietFrames;
        private int FireEnd => FireFrom + BssDirector.RippleFireFrames;
        private int SecondFireEnd => FireEnd + BssDirector.RippleFireFrames;

        private Vector2 anchor;
        private float groundY;
        /// <summary>本波已发射标记（按链序）</summary>
        private readonly bool[] fired = new bool[BssDirector.BodyCount + 2];
        /// <summary>预告波提示音已响标记（本地）</summary>
        private readonly bool[] ticked = new bool[BssDirector.BodyCount + 2];

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            anchor = ctx.Npc.Center;
            groundY = BssVfx.FindGroundY(anchor - new Vector2(0f, 60f));
            Array.Clear(fired);
            Array.Clear(ticked);
            ctx.RefreshSegments();
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            //拱身持位：前半身抬离地面，姿态即预告的画布
            ctx.Mode = BssMoveMode.Direct;
            ctx.LegCommand = BssLegCommand.Raise;
            float raise = MathHelper.Clamp(t / (float)ArchFrames, 0f, 1f);
            //P2 起就有回程波（尾→头），涟漪来回两趟才够场面
            bool secondPass = ctx.Phase >= 2;
            int allEnd = secondPass ? SecondFireEnd : FireEnd;
            if (t > allEnd) {
                raise = MathHelper.Clamp(1f - (t - allEnd) / 16f, 0f, 1f);
            }
            ctx.FrontRaise = raise * 0.7f;
            ctx.Compression = MathHelper.Lerp(1f, 0.94f, raise);

            Vector2 pose = new(anchor.X, groundY - BssDirector.CrawlRideHeight - 130f * raise);
            Vector2 desired = (pose - npc.Center) * 0.08f;
            if (desired.Length() > 6f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 6f;
            }
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.18f);
            npc.rotation = npc.rotation.AngleLerp(
                new Vector2(FacingToTarget(ctx), -0.8f).ToRotation() + BssHead.FacingRot, 0.1f);

            //波形声明
            if (t >= ArchFrames && t < TelegraphEnd) {
                ctx.PulseKind = 1;
                ctx.PulsePhase = (t - ArchFrames) / (float)BssDirector.RippleTelegraphFrames;
                TickTelegraph(ctx);
                ctx.BloomGlow = Math.Max(ctx.BloomGlow, 0.5f);
            }
            else if (t >= FireFrom && t < FireEnd) {
                ctx.PulseKind = 2;
                ctx.PulsePhase = (t - FireFrom) / (float)BssDirector.RippleFireFrames;
                FireWave(ctx, ctx.PulsePhase);
            }
            else if (secondPass && t >= FireEnd && t < SecondFireEnd) {
                if (t == FireEnd) {
                    Array.Clear(fired);
                }
                //P3 反向第二波：尾→头
                ctx.PulseKind = 2;
                ctx.PulsePhase = 1f - (t - FireEnd) / (float)BssDirector.RippleFireFrames;
                FireWave(ctx, ctx.PulsePhase);
            }

            Timer++;

            if (t > allEnd + 10 || t > 60 * 5) {
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>预告波扫过红花节的滴答（本地表现）</summary>
        private void TickTelegraph(BssStateContext ctx) {
            if (Main.dedServ || ctx.TotalSegments <= 0) {
                return;
            }
            foreach (var seg in ctx.Segments) {
                int ordinal = (int)seg.ai[0];
                if (ordinal >= ticked.Length || ticked[ordinal] || !BssStateContext.IsFlowerOrdinal(ordinal)) {
                    continue;
                }
                float fraction = ordinal / (float)ctx.TotalSegments;
                if (ctx.PulsePhase >= fraction) {
                    ticked[ordinal] = true;
                    //干木咔嗒逐节爬调
                    SoundEngine.PlaySound(SoundID.Item102 with { Volume = 0.5f, Pitch = 0.2f + fraction * 0.5f, MaxInstances = 4 },
                        seg.Center);
                }
            }
        }

        /// <summary>发射波：波前扫过红花节即朝体外上法向射钉刺（权威端），埋沙的节不开火</summary>
        private void FireWave(BssStateContext ctx, float phase) {
            if (ctx.TotalSegments <= 0) {
                return;
            }
            int bodyType = ModContent.NPCType<BssBody>();
            foreach (var seg in ctx.Segments) {
                if (!seg.Alives() || seg.type != bodyType) {
                    continue;
                }
                int ordinal = (int)seg.ai[0];
                if (ordinal >= fired.Length || fired[ordinal] || !BssStateContext.IsFlowerOrdinal(ordinal)) {
                    continue;
                }
                float fraction = ordinal / (float)ctx.TotalSegments;
                //正向波（第一轮）用 >=，反向波（P3 第二轮）用 <=
                bool crossed = Timer < FireEnd ? phase >= fraction : phase <= fraction;
                if (!crossed) {
                    continue;
                }
                fired[ordinal] = true;

                //埋沙的节不发射（看不见的炮口不算预告）
                if (!BssVfx.IsAboveGround(seg.Center)) {
                    continue;
                }

                //体外上法向
                float axis = seg.rotation + MathHelper.PiOver2;
                Vector2 normal = (axis + MathHelper.PiOver2).ToRotationVector2();
                if (normal.Y > 0f) {
                    normal = -normal;
                }

                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.5f, Pitch = 0.3f, MaxInstances = 4 }, seg.Center);
                    for (int i = 0; i < 4; i++) {
                        Dust d = Dust.NewDustPerfect(seg.Center + normal * 12f, DustID.JunglePlants,
                            normal.RotatedByRandom(0.4f) * Main.rand.NextFloat(1.5f, 3.5f), 100, default, 1f);
                        d.noGravity = true;
                    }
                }
                if (!VaultUtils.isClient) {
                    int damage = BssDirector.ScaleProjectileDamage(ctx.Npc, BssDirector.NeedleDamage);
                    int type = ModContent.ProjectileType<BssNeedleProj>();
                    int n = BssDirector.NeedlesPerFlower;
                    for (int i = 0; i < n; i++) {
                        float spread = MathHelper.Lerp(-BssDirector.NeedleFanHalf, BssDirector.NeedleFanHalf,
                            n > 1 ? i / (float)(n - 1) : 0.5f);
                        Vector2 vel = normal.RotatedBy(spread) * BssDirector.NeedleSpeed;
                        Projectile.NewProjectile(ctx.Npc.GetSource_FromAI(), seg.Center + normal * 14f,
                            vel, type, damage, 0.5f, Main.myPlayer);
                    }
                }
            }
        }
    }
}
