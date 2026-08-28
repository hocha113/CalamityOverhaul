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
    /// 上升泡幕（P2）：卷尾鼓泡三波，泡列从下方缓升。
    /// 声明式缺口：九列中每逢 (列号+波次) % 3 == 0 空列——每波三条开放走廊，
    /// 且随波次轮转（可学习的呼吸通道，由跳列语句而非注释保证）
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

        private float lockX;

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
                if (t == fire - 6) {
                    lockX = ctx.Target.Center.X;
                }
                if (t == fire) {
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 3 }, npc.Center);
                        ctx.TailFlare = 0.5f;
                    }
                    if (!VaultUtils.isClient) {
                        int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.BubbleDamage);
                        for (int k = -4; k <= 4; k++) {
                            //声明式缺口：随波次轮转的空列
                            if (((k + w) % 3 + 3) % 3 == 0) {
                                continue;
                            }
                            float laneX = lockX + k * LaneGap;
                            for (int j = 0; j < 3; j++) {
                                Vector2 spawn = new(laneX + (j - 1) * 12f,
                                    ctx.Target.Center.Y + 440f + j * 52f);
                                float radius = 19f + (((k + j) % 3 + 3) % 3) * 3.5f;
                                float rise = 2.8f + ((k + 9) % 2) * 0.5f;
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
