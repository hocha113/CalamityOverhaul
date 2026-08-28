using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>
    /// 上升泡幕（P2）：卷尾鼓泡三波，泡列快速上升（2026-08 重做：升速 4.4 档、
    /// 生成深度 330——威胁更快到位；奇数波列位错开半列距=交织压力；
    /// 封场存续时列位钳进双渊柱之间，走出泡幕即走进封场柱）。
    /// 声明式缺口：九列中每逢 (列号+波次) % 3 == 0 空列——每波三条开放走廊，
    /// 且随波次轮转（可学习的呼吸通道，由跳列语句而非注释保证）。
    /// 每波提前 30 帧锁列并生成车道预告实体（预告即承诺，缺口列不生成）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.BubbleCurtain, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpBubbleCurtainState : SeaShrimpStateBase
    {
        public override string StateName => "BubbleCurtain";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.BubbleCurtain;

        private static readonly int[] WaveFrames = [32, 62, 92];
        private const int Total = 130;
        /// <summary>列距 px（缺口宽度=列距-泡径）</summary>
        private const float LaneGap = 140f;
        /// <summary>锁列提前帧：预告实体的存在时长</summary>
        private const int LockLead = 30;
        /// <summary>泡列上升基速（重做：4.4 档，走两步躲不开了）</summary>
        private const float RiseSpeed = 4.4f;
        /// <summary>生成深度：距目标脚下的出生纵深</summary>
        private const float SpawnDepth = 330f;

        /// <summary>逐波锁定的列基准 X（波与波的锁帧交叠，不能共用一个标量）</summary>
        private readonly float[] laneLock = new float[3];

        /// <summary>缺口判定：随波次轮转的空列（生成与预告共用同一式）</summary>
        private static bool IsGapLane(int k, int w) => ((k + w) % 3 + 3) % 3 == 0;

        /// <summary>列位：奇数波整体错开半列距（交织走廊），生成与预告共用同一式</summary>
        private static float LaneX(float lockX, int k, int w)
            => lockX + k * LaneGap + (w % 2 == 1 ? LaneGap * 0.5f : 0f);

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            Timer++;
            HoldInPlace(ctx);

            float build = MathHelper.Clamp(t / 28f, 0f, 1f);
            ctx.SpineCurl = -0.5f * build;
            ctx.TailFlare = build;
            ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, build * 0.7f);
            ctx.WaveGain = 0.4f;

            for (int w = 0; w < WaveFrames.Length; w++) {
                int fire = WaveFrames[w];
                if (t == fire - LockLead) {
                    //锁列即承诺：预告实体按最终列位生成，此后不再追踪；
                    //封场存续时列基准钳进双渊柱之间（场地被封后走不出泡幕覆盖区）
                    float lockX = ctx.Target.Center.X;
                    if (ctx.ArenaActive) {
                        lockX = MathHelper.Clamp(lockX,
                            ctx.ArenaCenterX - SeaShrimpDirector.ArenaHalfWidth + 220f,
                            ctx.ArenaCenterX + SeaShrimpDirector.ArenaHalfWidth - 220f);
                    }
                    laneLock[w] = lockX;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = 0.4f, MaxInstances = 3 }, npc.Center);
                    }
                    if (!VaultUtils.isClient) {
                        for (int k = -4; k <= 4; k++) {
                            if (IsGapLane(k, w)) {
                                continue;
                            }
                            Projectile.NewProjectile(npc.GetSource_FromAI(),
                                new Vector2(LaneX(laneLock[w], k, w), ctx.Target.Center.Y), Vector2.Zero,
                                ModContent.ProjectileType<SeaShrimpLaneOmen>(), 0, 0f,
                                Main.myPlayer, LockLead);
                        }
                    }
                }
                if (t == fire) {
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 3 }, npc.Center);
                        ctx.TailFlare = 0.5f;
                        //出膛拍：尾扇处一口水花锥
                        Vector2 tailPos = ctx.Owner.Skeleton.Nodes[4].Pos;
                        EverdeepVFX.SplashBurst(tailPos, Vector2.UnitY * 7f, 0.8f);
                    }
                    if (!VaultUtils.isClient) {
                        int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.BubbleDamage);
                        for (int k = -4; k <= 4; k++) {
                            //声明式缺口：随波次轮转的空列（与预告同式）
                            if (IsGapLane(k, w)) {
                                continue;
                            }
                            float laneX = LaneX(laneLock[w], k, w);
                            for (int j = 0; j < 3; j++) {
                                Vector2 spawn = new(laneX + (j - 1) * 12f,
                                    ctx.Target.Center.Y + SpawnDepth + j * 52f);
                                float radius = 19f + (((k + j) % 3 + 3) % 3) * 3.5f;
                                float rise = RiseSpeed + ((k + 9) % 2) * 0.5f;
                                Projectile.NewProjectile(npc.GetSource_FromAI(), spawn, Vector2.Zero,
                                    ModContent.ProjectileType<SeaShrimpBubble>(), damage, 0.5f,
                                    Main.myPlayer, radius, rise);
                            }
                        }
                    }
                }
            }

            if (t >= Total) {
                return EndAttack(ctx, 60);
            }
            return null;
        }
    }
}
