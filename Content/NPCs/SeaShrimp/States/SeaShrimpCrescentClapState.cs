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
    /// 合钳泡群（P1+）：双螯先向两侧大张（蓄势可读）→ 对拍合击，
    /// 从钳口爆发一团细小泡群锥形涌向玩家（微弱追踪 55f 后直飞，
    /// 横向拉开即甩掉）。f24 锁向即承诺
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.CrescentClap, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpCrescentClapState : SeaShrimpStateBase
    {
        public override string StateName => "CrescentClap";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.CrescentClap;

        private const int SpreadEnd = 32;
        private const int LockFrame = 24;
        private const int ClapHold = 44;
        private const int Total = 64;

        private Vector2 lockDir = Vector2.UnitX;

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            Timer++;

            if (t < LockFrame) {
                lockDir = (PredictTarget(ctx, 9f) - npc.Center).SafeNormalize(Vector2.UnitX);
            }
            //蓄力期转身对线：钳口正对出刃方向
            float w = MathHelper.Clamp(t / (float)SpreadEnd, 0f, 1f);
            HoldFacing(ctx, lockDir.ToRotation(), MathHelper.Lerp(0.08f, 0.02f, w));

            Vector2 mouthPoint = npc.Center + lockDir * 120f;

            if (t < SpreadEnd) {
                //张钳蓄势：双螯向两侧大张，钳口全开（越张越满的可读预备）
                float spread = w * w * (3f - 2f * w);
                Vector2 lateral = lockDir.RotatedBy(MathHelper.PiOver2);
                for (int a = 0; a < 2; a++) {
                    float side = a == 0 ? 1f : -1f;
                    ctx.Claws[a] = new ClawDirective {
                        Mode = ClawMode.Hold,
                        Target = npc.Center + lockDir * 40f + lateral * side * (60f + spread * 120f),
                        Spring = 0.3f,
                        Damping = 0.7f,
                        ClawOpen = spread,
                    };
                }
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, w * 0.7f);
                float aimAlpha = MathHelper.Clamp((t - 8) / 14f, 0f, 1f) * (t >= LockFrame ? 0.55f : 0.3f);
                ctx.AddTelegraph(mouthPoint, lockDir, 520f, aimAlpha, t >= LockFrame ? 0.85f : 0.5f);
                if (t == 2 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.5f, Pitch = -0.15f, MaxInstances = 2 }, npc.Center);
                }
                return null;
            }

            if (t == SpreadEnd) {
                //对拍帧：双螯合击 + 泡群爆发 + 冲击环
                for (int a = 0; a < 2; a++) {
                    ctx.Owner.Skeleton.Arms[a].Impulse(lockDir * 30f);
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.75f, Pitch = 0.05f, MaxInstances = 2 }, mouthPoint);
                    SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.7f, Pitch = 0.2f, MaxInstances = 2 }, mouthPoint);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.85f, Pitch = -0.1f, MaxInstances = 2 }, mouthPoint);
                    ShakeNearby(npc.Center, 4.5f);
                    ctx.AddRing(mouthPoint, 150f, 20, 1f);
                    EverdeepVFX.SplashBurst(mouthPoint, -lockDir * 10f, 1f);
                }
                if (!VaultUtils.isClient) {
                    //泡群锥形爆发：速度与角度双错落（成团但不重叠），微追踪由泡自治
                    int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.SwarmBubbleDamage);
                    int count = SeaShrimpDirector.SwarmBubbleCount;
                    for (int i = 0; i < count; i++) {
                        float spread = MathHelper.Lerp(-0.55f, 0.55f, i / (count - 1f))
                            + Main.rand.NextFloat(-0.07f, 0.07f);
                        float speed = Main.rand.NextFloat(7f, 11.5f);
                        Vector2 vel = lockDir.RotatedBy(spread) * speed;
                        Projectile.NewProjectile(npc.GetSource_FromAI(),
                            mouthPoint + Main.rand.NextVector2Circular(26f, 26f), vel,
                            ModContent.ProjectileType<SeaShrimpSwarmBubble>(), damage, 0.5f, Main.myPlayer,
                            Main.rand.NextFloat(9f, 14f));
                    }
                }
            }

            if (t < ClapHold) {
                //合击持位：双螯并拢在钳口
                for (int a = 0; a < 2; a++) {
                    ctx.Claws[a] = new ClawDirective {
                        Mode = ClawMode.Strike,
                        Target = mouthPoint,
                        Spring = 0.5f,
                        Damping = 0.82f,
                        ClawOpen = 0f,
                    };
                }
                ctx.AfterimageStrength = MathF.Max(ctx.AfterimageStrength, 0.4f);
                return null;
            }

            if (t >= Total) {
                return EndAttack(ctx, 44);
            }
            return null;
        }
    }
}
