using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>
    /// 泡泡棒球（P1+）：蝎式卷尾一记挥甩，甩出四个带电待拍泡漂在头前扇区 →
    /// 双螯交替每 15f 一记猛拍，把泡逐个轰向玩家（拍击帧锁预测点，直线不追踪）。
    /// 被拍泡起爆时与上一爆点互连闪电——链沿玩家走位轨迹铺开。
    /// 拍击音调逐拍渐升（节奏推进的听觉声明），单拍可侧移躲
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.BubbleBat, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpBubbleBatState : SeaShrimpStateBase
    {
        public override string StateName => "BubbleBat";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.BubbleBat;

        /// <summary>卷尾蓄力结束、挥甩帧</summary>
        private const int FlingFrame = 24;
        /// <summary>待拍泡数</summary>
        private const int BubbleCount = 4;
        /// <summary>拍间隔帧</summary>
        private const int BatInterval = 15;
        /// <summary>首拍帧</summary>
        private const int FirstBat = 36;
        private const int Total = FirstBat + BatInterval * BubbleCount + 16;

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            Timer++;

            //对线慢转：拍段身体稳住，泡与玩家保持大致同侧
            Vector2 toTarget = (ctx.Target.Center - npc.Center).SafeNormalize(Vector2.UnitX);
            HoldFacing(ctx, toTarget.ToRotation(), 0.03f);

            float heading = ctx.Owner.Locomotion.Heading;
            Vector2 forward = heading.ToRotationVector2();

            if (t <= FlingFrame) {
                //蝎式卷尾蓄力：尾扇甩到背上方、扇面全张
                float curlIn = MathHelper.Clamp(t / (float)(FlingFrame - 4), 0f, 1f);
                ctx.SpineCurl = -0.9f * (curlIn * curlIn * (3f - 2f * curlIn));
                ctx.TailFlare = curlIn;
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, curlIn * 0.6f);
                ctx.WaveGain = 0.35f;
                if (t == 2 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 2 }, npc.Center);
                }
            }

            if (t == FlingFrame) {
                //挥尾出泡：一记甩出四个待拍泡，弧形漂向头前扇区停住
                Vector2 tailPos = ctx.Owner.Skeleton.Nodes[4].Pos;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item102 with { Volume = 0.7f, Pitch = -0.2f, MaxInstances = 2 }, npc.Center);
                    EverdeepVFX.SplashBurst(tailPos, forward * 8f - Vector2.UnitY * 4f, 1f);
                    ShakeNearby(npc.Center, 3f);
                    //挥尾水沫：沿甩弧撒一片碎滴与拉伸水团——"甩出大量泡泡"的画面填充
                    Vector2 tailFwd = ctx.Owner.Skeleton.Nodes[4].Forward;
                    for (int i = 0; i < 14; i++) {
                        float arc = MathHelper.Lerp(-0.7f, 0.7f, i / 13f);
                        Vector2 dir = (tailFwd.ToRotation() + arc).ToRotationVector2();
                        EverdeepVFX.ShedDroplet(tailPos + dir * Main.rand.NextFloat(6f, 30f),
                            dir * Main.rand.NextFloat(2.5f, 6f) - Vector2.UnitY * Main.rand.NextFloat(0.5f, 2f), 1f);
                    }
                    for (int i = 0; i < 5; i++) {
                        Vector2 dir = (tailFwd.ToRotation() + Main.rand.NextFloat(-0.6f, 0.6f)).ToRotationVector2();
                        PRTLoader.NewParticle<PRT_AbyssGlob>(tailPos + dir * 10f, dir * Main.rand.NextFloat(3f, 7f),
                            Color.Lerp(SeaShrimpVFX.Deep, SeaShrimpVFX.Body, Main.rand.NextFloat()),
                            Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(14, 22), 1.7f);
                    }
                }
                if (!VaultUtils.isClient) {
                    int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.SparkBubbleDamage);
                    int chainId = SeaShrimpSparkBubble.MakeChainId(npc.whoAmI, ctx.AttackIndex);
                    for (int i = 0; i < BubbleCount; i++) {
                        //槽位：头前上方扇形弧（间距由角距+径距共同拉开）
                        float slotAngle = heading + MathHelper.Lerp(-0.55f, 0.55f, i / (float)(BubbleCount - 1));
                        float slotDist = 185f + (i % 2) * 55f;
                        Vector2 slot = npc.Center + slotAngle.ToRotationVector2() * slotDist - Vector2.UnitY * 40f;
                        //初速按纯阻尼(×0.94)积分距离反算：泡自然漂到槽位附近停住
                        Vector2 vel = (slot - tailPos) / 16.7f;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), tailPos, vel,
                            ModContent.ProjectileType<SeaShrimpSparkBubble>(), damage, 1f, Main.myPlayer,
                            SeaShrimpSparkBubble.HeldBurstAge + 100, chainId, SeaShrimpDirector.BatBubbleRadius);
                    }
                }
            }

            if (t > FlingFrame && t <= FlingFrame + 8) {
                //挥甩反坐：脊柱前抖回中
                float r = (t - FlingFrame) / 8f;
                ctx.SpineCurl = MathHelper.Lerp(0.25f, 0f, r);
                ctx.TailFlare = MathHelper.Lerp(0.5f, 0.35f, r);
            }

            //连拍段：每 15f 一拍，出拍钳按泡所在身侧选（不跨身别扭挥拍）
            int chainKey = SeaShrimpSparkBubble.MakeChainId(npc.whoAmI, ctx.AttackIndex);
            for (int k = 0; k < BubbleCount; k++) {
                int batFrame = FirstBat + k * BatInterval;

                if (t >= batFrame - 8 && t < batFrame) {
                    //拍前预备：钳撤到泡后方上膛，预告线亮（泡→玩家预测点）
                    Projectile bubble = FindHeldBubble(chainKey);
                    if (bubble != null) {
                        int arm = PickArm(ctx, bubble.Center);
                        Vector2 aim = (PredictTarget(ctx, 10f) - bubble.Center).SafeNormalize(forward);
                        ctx.Claws[arm] = new ClawDirective {
                            Mode = ClawMode.Hold,
                            Target = bubble.Center - aim * 76f,
                            Spring = 0.4f,
                            Damping = 0.72f,
                            ClawOpen = 0.65f,
                        };
                        float a = (t - (batFrame - 8)) / 8f * 0.5f;
                        ctx.AddTelegraph(bubble.Center, aim, 640f, a, 0.7f);
                        ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, 0.5f);
                    }
                }

                if (t == batFrame) {
                    //拍击帧：钳冲量砸穿泡位，泡被轰向预测点（锁定即承诺）
                    Projectile bubble = FindHeldBubble(chainKey);
                    if (bubble != null) {
                        int arm = PickArm(ctx, bubble.Center);
                        Vector2 aim = (PredictTarget(ctx, 10f) - bubble.Center).SafeNormalize(forward);
                        ctx.Owner.Skeleton.Arms[arm].Impulse(aim * 42f);
                        ctx.Claws[arm] = new ClawDirective {
                            Mode = ClawMode.Strike,
                            Target = bubble.Center,
                            Spring = 0.6f,
                            Damping = 0.85f,
                            ClawOpen = 0f,
                        };
                        if (!Main.dedServ) {
                            //音调逐拍渐升：连拍推进的听觉声明
                            SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.75f, Pitch = -0.1f + k * 0.13f, MaxInstances = 3 }, bubble.Center);
                            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = 0.1f, MaxInstances = 3 }, bubble.Center);
                            ShakeNearby(npc.Center, 2.6f);
                            ctx.AddRing(bubble.Center, 86f, 16, 1f);
                            EverdeepVFX.SplashBurst(bubble.Center, aim * 9f, 0.8f);
                        }
                        if (!VaultUtils.isClient) {
                            float dist = Vector2.Distance(ctx.Target.Center, bubble.Center);
                            float flight = MathHelper.Clamp(dist / SeaShrimpDirector.BattedBubbleSpeed, 12f, 48f);
                            bubble.velocity = aim * SeaShrimpDirector.BattedBubbleSpeed;
                            bubble.ai[0] = (int)bubble.localAI[0] + flight;
                            bubble.netUpdate = true;
                        }
                        ctx.AfterimageStrength = MathF.Max(ctx.AfterimageStrength, 0.55f);
                    }
                }
            }

            if (t >= Total) {
                return EndAttack(ctx, 50);
            }
            return null;
        }

        /// <summary>
        /// 找下一个待拍泡：本次出招链上、仍在待拍、identity 最小者。
        /// identity 随生成包同步，各端选择一致（客户端演钳、权威端写弹）
        /// </summary>
        private static Projectile FindHeldBubble(int chainId) {
            Projectile best = null;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.ModProjectile is not SeaShrimpSparkBubble
                    || (int)proj.ai[1] != chainId
                    || proj.ai[0] < SeaShrimpSparkBubble.HeldBurstAge) {
                    continue;
                }
                if (best == null || proj.identity < best.identity) {
                    best = proj;
                }
            }
            return best;
        }

        /// <summary>出拍钳选侧：泡在哪侧就用哪侧的螯（各端从一致的泡位求得同一答案）</summary>
        private static int PickArm(SeaShrimpStateContext ctx, Vector2 bubblePos) {
            Vector2 lateral0 = ctx.Owner.Skeleton.Lateral(0);
            return Vector2.Dot(bubblePos - ctx.Npc.Center, lateral0) >= 0f ? 0 : 1;
        }
    }
}
