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
    /// 天游：破空而起，长时间在玩家上方蛇形游荡（利萨如轨迹，游龙身段），
    /// 游荡中按拍喷沙团、P2 起洒花瓣，收尾锁点俯冲砸地喷发。
    /// 公平阀：游荡锚点始终偏离玩家本体（压迫来自蜿蜒的身体而非追尾）；
    /// 喷沙每拍预亮 10 帧且出手即锁向；俯冲预告 20 帧锁点不追瞄；砸地喷发沿用逃生道声明。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.SkyWeave, typeof(BssStateContext))]
    internal class BssSkyWeaveState : BssStateBase
    {
        public override string StateName => "SkyWeave";
        public override BssStateIndex StateIndex => BssStateIndex.SkyWeave;

        private const int LeapFrame = 10;
        private const int LoiterFrom = 12;
        private int LoiterEnd => LoiterFrom + BssDirector.WeaveDuration;
        private int DiveFrame => LoiterEnd + BssDirector.WeaveDiveTelegraph;

        /// <summary>本拍喷沙锁向（预亮拍存向，出手拍使用）</summary>
        private Vector2 burstAim = Vector2.UnitX;
        /// <summary>俯冲锁点（预告起始帧锁定，不追瞄）</summary>
        private Vector2 diveTarget;
        private bool dived;
        private bool landed;
        private float landTime;
        private float prevY;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            dived = false;
            landed = false;
            prevY = ctx.Npc.Center.Y;
            ctx.RefreshSegments();
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            float seed = npc.whoAmI * 0.73f;

            if (t < LeapFrame) {
                //蹬地蓄势：前身压低（弹簧要先压才有劲）
                ctx.Mode = BssMoveMode.Crawl;
                ctx.CrawlSpeed = 4f;
                ctx.CrawlDirX = FacingToTarget(ctx);
                ctx.FrontRaise = 0.3f;
            }
            else if (t == LeapFrame) {
                //破空：一帧起跳
                if (!VaultUtils.isClient) {
                    float dir = FacingToTarget(ctx, 0f);
                    npc.velocity = new Vector2(dir * 9f, -26f);
                    npc.netUpdate = true;
                }
                ctx.PulseWhip(10f);
                if (!Main.dedServ) {
                    BssVfx.SandBurst(npc.Bottom, 1.1f);
                    BssVfx.Roar(npc.Center, -0.2f, 0.8f);
                }
                ctx.Mode = BssMoveMode.Direct;
                ctx.LegCommand = BssLegCommand.Flail;
            }
            else if (t < LoiterEnd) {
                //游荡：利萨如锚点绕顶蜿蜒（锚点偏离玩家本体 = 压迫来自身体不来自追尾）
                float wt = t - LoiterFrom;
                Vector2 anchor = ctx.Target.Center + new Vector2(
                    MathF.Sin(wt * 0.03f + seed) * 440f,
                    -300f + MathF.Sin(wt * 0.0175f + seed * 1.7f) * 140f);
                ctx.Mode = BssMoveMode.Steer;
                ctx.MoveTarget = anchor;
                ctx.MoveSpeed = BssDirector.WeaveSpeed;
                ctx.TurnSpeed = 2.6f;
                ctx.AccelRate = 0.1f;
                ctx.Slither = 0.85f;
                ctx.LegCommand = BssLegCommand.Flail;

                //身体即威胁：高速蜿蜒段开伤害窗
                if (npc.velocity.Length() > 14f) {
                    npc.damage = npc.defDamage;
                }

                UpdateWeaveBursts(ctx, npc, t);
                UpdateWeavePetals(ctx, npc, t);
            }
            else if (t < DiveFrame) {
                //俯冲预告：锁点、亮花、后仰吸气（反向运动）
                if (t == LoiterEnd) {
                    Vector2 predicted = ctx.Target.Center + ctx.Target.velocity * 14f;
                    diveTarget = new Vector2(predicted.X, BssVfx.FindGroundY(predicted - new Vector2(0f, 100f)));
                    if (!Main.dedServ) {
                        BssVfx.Roar(npc.Center, -0.55f, 1f);
                    }
                }
                ctx.Mode = BssMoveMode.Direct;
                npc.velocity = Vector2.Lerp(npc.velocity,
                    (npc.Center - diveTarget).SafeNormalize(-Vector2.UnitY) * 5f, 0.2f);
                npc.rotation = npc.rotation.AngleLerp(
                    (diveTarget - npc.Center).ToRotation() + BssHead.FacingRot, 0.2f);
                ctx.BloomGlow = Math.Max(ctx.BloomGlow, 1f);
                ctx.LegCommand = BssLegCommand.Tuck;
            }
            else {
                //俯冲与砸地
                if (!dived) {
                    dived = true;
                    if (!VaultUtils.isClient) {
                        npc.velocity = (diveTarget - npc.Center).SafeNormalize(Vector2.UnitY)
                            * BssDirector.WeaveDiveSpeed;
                        npc.netUpdate = true;
                    }
                    ctx.PulseWhip(12f);
                }
                ctx.Mode = BssMoveMode.Direct;
                ctx.LegCommand = BssLegCommand.Tuck;
                float speed = npc.velocity.Length();
                if (speed > BssDirector.LungeContactSpeed) {
                    npc.damage = npc.defDamage;
                }

                //入地检测：砸地喷发 + 转爬行收招
                float groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 300f), 900f);
                if (!landed && prevY < groundY && npc.Center.Y >= groundY - 10f) {
                    landed = true;
                    landTime = Timer;
                    ctx.PulseWhip(9f);
                    if (!Main.dedServ) {
                        BssVfx.SandBurst(new Vector2(npc.Center.X, groundY), 1.6f);
                        BssVfx.Shake(npc.Center, 7f);
                    }
                    BssVfx.BreachEruption(npc, new Vector2(npc.Center.X, groundY), 7);
                }
                if (landed && Timer - landTime > 10f) {
                    npc.velocity *= 0.5f;
                    return EndAttack(ctx);
                }
            }

            prevY = npc.Center.Y;
            Timer++;

            if (t > 60 * 10) {
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>游荡喷沙：每拍预亮 10 帧存向，出手拍按锁向三连喷（锁向 = 不追瞄）</summary>
        private void UpdateWeaveBursts(BssStateContext ctx, NPC npc, int t) {
            int cycle = (t - LoiterFrom) % BssDirector.WeaveSpitGap;
            if (cycle == 0) {
                burstAim = (ctx.Target.Center - npc.Center).SafeNormalize(Vector2.UnitX);
            }
            if (cycle < 10) {
                ctx.BloomGlow = Math.Max(ctx.BloomGlow, 0.6f);
                if (!Main.dedServ && Main.GameUpdateCount % 2 == 0) {
                    Vector2 mouth = npc.Center + burstAim * 28f;
                    Vector2 from = mouth + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(24f, 46f);
                    Dust d = Dust.NewDustPerfect(from, DustID.Sand, (mouth - from) * 0.14f, 120, default, 0.9f);
                    d.noGravity = true;
                }
                return;
            }
            if (cycle != 10) {
                return;
            }

            Vector2 muzzle = npc.Center + burstAim * 28f;
            npc.velocity -= burstAim * 2.4f;
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.65f, Pitch = -0.15f, MaxInstances = 3 }, muzzle);
            }
            if (!VaultUtils.isClient) {
                int damage = BssDirector.ScaleProjectileDamage(npc, BssDirector.SandGlobDamage);
                int type = ModContent.ProjectileType<BssSandGlob>();
                for (int i = -1; i <= 1; i++) {
                    Vector2 vel = burstAim.RotatedBy(i * 0.14f) * BssDirector.SandGlobSpeed
                        + new Vector2(0f, -1f);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, vel, type, damage, 0.6f, Main.myPlayer);
                }
            }
        }

        /// <summary>游荡洒瓣（P2 起）：每拍每朵红花落 1 瓣，随风缓降成幕</summary>
        private void UpdateWeavePetals(BssStateContext ctx, NPC npc, int t) {
            if (ctx.Phase < 2 || (t - LoiterFrom) % BssDirector.WeavePetalGap != 0) {
                return;
            }
            int bodyType = ModContent.NPCType<BssBody>();
            foreach (var seg in ctx.Segments) {
                if (!seg.Alives() || seg.type != bodyType
                    || !BssStateContext.IsFlowerOrdinal((int)seg.ai[0])) {
                    continue;
                }
                if (!Main.dedServ) {
                    BssVfx.PetalDrift(seg.Center, new Vector2(ctx.WindSign * 1.2f, 0.6f));
                }
                if (!VaultUtils.isClient) {
                    int damage = BssDirector.ScaleProjectileDamage(npc, BssDirector.PetalDamage);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), seg.Center,
                        new Vector2(Main.rand.NextFloat(-1f, 1f), 1.2f),
                        ModContent.ProjectileType<BssPetalProj>(), damage, 0.4f, Main.myPlayer,
                        ctx.WindSign, Main.rand.NextFloat(MathHelper.TwoPi));
                }
            }
        }
    }
}
