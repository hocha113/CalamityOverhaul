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
    /// 空泡拳（签名招）：扎稳站姿 → 50f 聚能（向心晶光流，72% 处截停进入静默拍）
    /// → 迟滞后拉猛然出拳 → 拳锋处留下一颗生长空泡，一拍之后爆缩闪光二次判定。
    /// 双拍节奏：躲拳，再躲拳后的气泡。f32 锁向即承诺
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.CavitationPunch, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpCavitationPunchState : SeaShrimpStateBase
    {
        public override string StateName => "CavitationPunch";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.CavitationPunch;

        private const int ChargeEnd = 50;
        private const int LockFrame = 32;
        /// <summary>聚能粒子截停点（吸气拍起点）</summary>
        private const int QuietFrame = 36;
        private const int PunchEnd = 60;
        private const int Total = 88;

        private Vector2 lockDir = Vector2.UnitX;

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            Timer++;
            HoldInPlace(ctx);

            Vector2 shoulder = ctx.Owner.Skeleton.ShoulderWorld(0);

            if (t < LockFrame) {
                lockDir = (PredictTarget(ctx, 10f) - shoulder).SafeNormalize(Vector2.UnitX);
            }

            if (t < ChargeEnd) {
                float w = t / (float)ChargeEnd;
                //迟滞后拉：前段几乎不动，最后几帧猛地吸回（MOTION 反向语法）
                float reel = MathF.Pow(w, 8f) * 64f;
                ctx.Claws[0] = new ClawDirective {
                    Mode = ClawMode.Hold,
                    Target = shoulder - lockDir * (10f + reel),
                    Spring = 0.34f,
                    Damping = 0.68f,
                    ClawOpen = 0f,
                };
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, w);
                ctx.SpineCurl = -0.12f * w;

                //向心晶光流：密度随聚能升，72% 截停（爆前静默）
                if (!Main.dedServ && t < QuietFrame && t % 2 == 0) {
                    Vector2 claw = ctx.Owner.Skeleton.ClawTip(0);
                    Vector2 from = claw + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(50f, 110f);
                    PRTLoader.NewParticle<PRT_Spark>(from, (claw - from) * 0.11f,
                        Color.Lerp(SeaShrimpRenderer.CrystalBlue, Color.White, Main.rand.NextFloat(0.35f)),
                        Main.rand.NextFloat(0.4f, 0.75f))?.Configure(false, Main.rand.Next(9, 14));
                }
                if (t == 4 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.55f, Pitch = -0.4f, MaxInstances = 2 }, shoulder);
                }

                float aimAlpha = MathHelper.Clamp((t - 14) / 22f, 0f, 1f) * (t >= LockFrame ? 0.6f : 0.28f);
                ctx.AddSolidBeam(shoulder + lockDir * 26f, lockDir,
                    SeaShrimpDirector.PunchReach + 130f, aimAlpha, t >= LockFrame ? 0.9f : 0.5f);
                return null;
            }

            if (t == ChargeEnd) {
                //出拳帧：冲量 + 本体反冲 + 定向震屏
                ctx.Owner.Skeleton.Arms[0].Impulse(lockDir * 50f);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.85f, Pitch = -0.1f, MaxInstances = 2 }, shoulder);
                    ShakeNearby(npc.Center, 7f);
                }
                if (!VaultUtils.isClient) {
                    int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.ClawStrikeDamage);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), shoulder, Vector2.Zero,
                        ModContent.ProjectileType<SeaShrimpClawHitbox>(), damage, 4f, Main.myPlayer,
                        npc.whoAmI, 0f);
                }
            }

            if (t == ChargeEnd + 4 && !VaultUtils.isClient) {
                //拳锋落泡：第二拍主角（生长期即预告，爆缩才有伤害）
                int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.CavitationDamage);
                Projectile.NewProjectile(npc.GetSource_FromAI(),
                    ctx.Owner.Skeleton.ShoulderWorld(0) + lockDir * SeaShrimpDirector.PunchReach,
                    Vector2.Zero, ModContent.ProjectileType<SeaShrimpCavitationBubble>(), damage, 2f,
                    Main.myPlayer, SeaShrimpDirector.CavitationCollapseDelay,
                    SeaShrimpDirector.CavitationBubbleRadius);
            }

            if (t < PunchEnd) {
                ctx.Claws[0] = new ClawDirective {
                    Mode = ClawMode.Strike,
                    Target = ctx.Owner.Skeleton.ShoulderWorld(0) + lockDir * SeaShrimpDirector.PunchReach,
                    Spring = 0.6f,
                    Damping = 0.85f,
                    ClawOpen = 0f,
                };
                ctx.ClawDamageWindow = t >= ChargeEnd + 2 && t <= ChargeEnd + 8;
                //出拳反坐：脊柱后仰一口
                ctx.SpineCurl = 0.22f * (1f - (t - ChargeEnd) / (float)(PunchEnd - ChargeEnd));
                ctx.AfterimageStrength = MathF.Max(ctx.AfterimageStrength, 0.6f);
                return null;
            }

            if (t >= Total) {
                return EndAttack(ctx, 58);
            }
            return null;
        }
    }
}
