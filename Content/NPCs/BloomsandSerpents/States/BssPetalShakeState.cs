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
    /// 抖擞花瓣（P2 起）：足端踩定、全身在腿上抖，红花节甩出缓降花瓣，随沙暴风漂移。
    /// 公平阀声明：花瓣只从红花节出（花道间距 ≈ FlowerStep×节距）、出生横向抖动上限
    /// PetalLaneHalfWidth=26 = 花道之间保有可站走廊；缓降 + 风向固定（出手锁定），可预读。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.PetalShake, typeof(BssStateContext))]
    internal class BssPetalShakeState : BssStateBase
    {
        public override string StateName => "PetalShake";
        public override BssStateIndex StateIndex => BssStateIndex.PetalShake;

        private const int HaltFrames = 8;
        private int BeatLen => BssDirector.ShakeWindup + BssDirector.ShakeBurst + BssDirector.ShakeRest;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            ctx.RefreshSegments();
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            int beats = ctx.Phase >= 3 ? BssDirector.ShakeBeats + 1 : BssDirector.ShakeBeats;
            int allEnd = HaltFrames + beats * BeatLen;

            //盘拢站定：足端踩住，身体缩紧
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlSpeed = 0f;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.LegCommand = BssLegCommand.March;
            ctx.Compression = 0.85f;

            if (t >= HaltFrames && t < allEnd) {
                int bt = (t - HaltFrames) % BeatLen;
                int beat = (t - HaltFrames) / BeatLen;

                if (bt < BssDirector.ShakeWindup) {
                    //蓄势：花光渐亮 + 草叶窸窣
                    ctx.BloomGlow = Math.Max(ctx.BloomGlow, bt / (float)BssDirector.ShakeWindup * 0.8f);
                    DeclareJaw(ctx, BssJawCommand.Inhale, bt / (float)BssDirector.ShakeWindup);
                    if (bt == 0 && !Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 3 }, npc.Center);
                        if (beat == 0) {
                            BssVfx.Roar(npc.Center, 0.05f, 0.55f);
                        }
                    }
                }
                else if (bt < BssDirector.ShakeWindup + BssDirector.ShakeBurst) {
                    //抖动拍：身体在腿上剧颤（绘制层偏移，位置不动）
                    ctx.ShakeStrength = 1f;
                    ctx.BloomGlow = Math.Max(ctx.BloomGlow, 0.9f);
                    DeclareJaw(ctx, BssJawCommand.Spit);
                    int burstT = bt - BssDirector.ShakeWindup;
                    //每拍甩 PetalsPerFlower 轮（间隔 4 帧错开，瓣幕有厚度不糊团）
                    if (burstT % 4 == 0 && burstT < BssDirector.PetalsPerFlower * 4) {
                        SpawnPetals(ctx);
                    }
                    if (burstT == 0) {
                        ctx.JawBurst = 1f;
                        ctx.PulseWhip(6f);
                        if (!Main.dedServ) {
                            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = 0.15f, MaxInstances = 3 }, npc.Center);
                            BssVfx.Shake(npc.Center, 2.5f, 1000f);
                        }
                    }
                }
                //歇止段：什么都不做（拍与拍之间的呼吸）
            }

            Timer++;

            if (t > allEnd + 12 || t > 60 * 4) {
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>红花节甩瓣：出生点横向抖动封顶在 PetalLaneHalfWidth（走廊声明），权威端裁决</summary>
        private static void SpawnPetals(BssStateContext ctx) {
            int bodyType = ModContent.NPCType<BssBody>();
            foreach (var seg in ctx.Segments) {
                if (!seg.Alives() || seg.type != bodyType) {
                    continue;
                }
                int ordinal = (int)seg.ai[0];
                if (!BssStateContext.IsFlowerOrdinal(ordinal) || !BssVfx.IsAboveGround(seg.Center)) {
                    continue;
                }

                float axis = seg.rotation + MathHelper.PiOver2;
                Vector2 axisDir = axis.ToRotationVector2();
                Vector2 normal = (axis + MathHelper.PiOver2).ToRotationVector2();
                if (normal.Y > 0f) {
                    normal = -normal;
                }

                //表现瓣（客户端）
                if (!Main.dedServ) {
                    for (int i = 0; i < 2; i++) {
                        BssVfx.PetalDrift(seg.Center + normal * 10f,
                            normal * Main.rand.NextFloat(1f, 2f) + axisDir * Main.rand.NextFloat(-1f, 1f));
                    }
                }
                //伤害瓣（权威端）
                if (!VaultUtils.isClient) {
                    int damage = BssDirector.ScaleProjectileDamage(ctx.Npc, BssDirector.PetalDamage);
                    int type = ModContent.ProjectileType<BssPetalProj>();
                    Vector2 pos = seg.Center + normal * 10f
                        + axisDir * Main.rand.NextFloat(-BssDirector.PetalLaneHalfWidth, BssDirector.PetalLaneHalfWidth);
                    Vector2 vel = normal * Main.rand.NextFloat(1.8f, 3f) + axisDir * Main.rand.NextFloat(-1.1f, 1.1f);
                    Projectile.NewProjectile(ctx.Npc.GetSource_FromAI(), pos, vel, type, damage, 0.4f,
                        Main.myPlayer, ctx.WindSign, Main.rand.NextFloat(MathHelper.TwoPi));
                }
            }
        }
    }
}
