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
    /// 泡泡大炮（P1+）：双螯向两侧大张 → 钳口聚水聚电、巨型雷泡迅速膨胀
    /// （f34 粒子截停，爆前静默拍）→ 聚钳一记对拍把泡轰向玩家（拍击瞬间记录位置 A，
    /// 锁点即承诺不追踪）→ 泡到 A 崩爆散出一圈带电小泡，错帧链爆互连闪电。
    /// 泡的飞行与链爆全程弹幕自治，本体拍完立即收招回 hub——出手迅猛不等演出
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.BubbleCannon, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpBubbleCannonState : SeaShrimpStateBase
    {
        public override string StateName => "BubbleCannon";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.BubbleCannon;

        /// <summary>泡生成帧（与 VoltBubble.GrowFrames 合拍：f44 恰好长满）</summary>
        private const int SpawnFrame = 12;
        /// <summary>聚能粒子截停帧（吸气拍起点）</summary>
        private const int QuietFrame = 34;
        /// <summary>拍击帧</summary>
        private const int SlamFrame = 44;
        private const int Total = 68;

        private Vector2 lockDir = Vector2.UnitX;

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            Timer++;

            //对线追踪到拍击帧才最终锁定（预告线全程跟踪，A 点在出手瞬间记录）
            if (t < SlamFrame) {
                lockDir = (ctx.Target.Center - npc.Center).SafeNormalize(Vector2.UnitX);
            }
            float w = MathHelper.Clamp(t / (float)SlamFrame, 0f, 1f);
            HoldFacing(ctx, lockDir.ToRotation(), MathHelper.Lerp(0.08f, 0.02f, w));

            float heading = ctx.Owner.Locomotion.Heading;
            Vector2 forward = heading.ToRotationVector2();
            Vector2 muzzle = npc.Center + forward * SeaShrimpVoltBubble.MuzzleOffset;
            Vector2 lateral = forward.RotatedBy(MathHelper.PiOver2);

            if (t < SlamFrame) {
                //张钳蓄势：双螯向两侧大张、钳口全开（越张越满的可读预备）
                float spread = w * w * (3f - 2f * w);
                for (int a = 0; a < 2; a++) {
                    float side = a == 0 ? 1f : -1f;
                    ctx.Claws[a] = new ClawDirective {
                        Mode = ClawMode.Hold,
                        Target = npc.Center + forward * 30f + lateral * side * (70f + spread * 130f),
                        Spring = 0.3f,
                        Damping = 0.7f,
                        ClawOpen = spread,
                    };
                }
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, w);
                ctx.SpineCurl = -0.1f * w;

                if (t == SpawnFrame && !VaultUtils.isClient) {
                    //钳口落泡：生长期无害，泡体膨胀即预告（链 id 每次出招独立，跨招不串）
                    int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.VoltBubbleDamage);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, Vector2.Zero,
                        ModContent.ProjectileType<SeaShrimpVoltBubble>(), damage, 3f, Main.myPlayer,
                        SeaShrimpSparkBubble.MakeChainId(npc.whoAmI, ctx.AttackIndex), 0f, 0f);
                }

                if (t == 4 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.6f, Pitch = -0.35f, MaxInstances = 2 }, muzzle);
                }
                if (t == 22 && !Main.dedServ) {
                    //电流上膛：泡内电弧开始游走的听觉声明
                    SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.45f, Pitch = -0.2f, MaxInstances = 2 }, muzzle);
                }

                //向心聚能：大量水团+电火花+碎滴被吸进泡里，密度∝√涨压，f34 截停——爆前静默
                if (!Main.dedServ && t >= SpawnFrame && t < QuietFrame) {
                    float charge = (t - SpawnFrame) / (float)(QuietFrame - SpawnFrame);
                    int globs = Main.rand.NextFloat() < 0.4f + 0.4f * MathF.Sqrt(charge) ? 2 : 1;
                    for (int i = 0; i < globs; i++) {
                        Vector2 rim = muzzle + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(170f, 300f);
                        PRTLoader.NewParticle<PRT_AbyssGlob>(rim, (muzzle - rim) * 0.09f,
                            Color.Lerp(SeaShrimpVFX.Deep, SeaShrimpVFX.Body, Main.rand.NextFloat()),
                            Main.rand.NextFloat(0.3f, 0.55f))?.Configure(13, 1.9f);
                    }
                    if (Main.rand.NextFloat() < 0.4f + 0.5f * charge) {
                        Vector2 rim = muzzle + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(120f, 230f);
                        PRTLoader.NewParticle<PRT_AbyssSpark>(rim, (muzzle - rim) * 0.11f,
                            SeaShrimpBubbleArc.ArcColor, Main.rand.NextFloat(0.35f, 0.65f))?.Configure(10);
                    }
                    //切向碎滴：向心流带一点旋涌，聚拢不只是直线吸
                    if (Main.rand.NextFloat() < 0.3f + 0.4f * charge) {
                        Vector2 rim = muzzle + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(90f, 190f);
                        Vector2 inward = (muzzle - rim) * 0.07f;
                        EverdeepVFX.ShedDroplet(rim, inward + inward.RotatedBy(MathHelper.PiOver2) * 0.6f, 0.9f);
                    }
                }

                //锁向线：泡将飞向的方向（跟踪显示，拍击帧定格）
                float aimAlpha = MathHelper.Clamp((t - 24) / 16f, 0f, 1f) * 0.5f;
                if (aimAlpha > 0f) {
                    ctx.AddTelegraph(muzzle + forward * 40f, lockDir, 720f, aimAlpha, 0.7f);
                }
                return null;
            }

            if (t == SlamFrame) {
                //对拍帧：双螯合击 + 冲击环 + 记录 A 点写泡速度（锁点即承诺）
                for (int a = 0; a < 2; a++) {
                    ctx.Owner.Skeleton.Arms[a].Impulse(forward * 36f);
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.9f, Pitch = -0.05f, MaxInstances = 2 }, muzzle);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.85f, Pitch = -0.2f, MaxInstances = 2 }, muzzle);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 2 }, muzzle);
                    ShakeNearby(npc.Center, 6f);
                    ctx.AddRing(muzzle, 200f, 22, 1f);
                    EverdeepVFX.SplashBurst(muzzle, forward * 12f, 1.1f);
                }
                //本体反冲：泡的质量顶回来一口
                npc.velocity -= forward * 4f;

                if (!VaultUtils.isClient) {
                    int chainId = SeaShrimpSparkBubble.MakeChainId(npc.whoAmI, ctx.AttackIndex);
                    foreach (Projectile proj in Main.ActiveProjectiles) {
                        if (proj.ModProjectile is not SeaShrimpVoltBubble
                            || (int)proj.ai[0] != chainId || proj.ai[1] >= 1f) {
                            continue;
                        }
                        Vector2 target = ctx.Target.Center;
                        Vector2 dir = (target - proj.Center).SafeNormalize(forward);
                        float dist = Vector2.Distance(target, proj.Center);
                        proj.velocity = dir * SeaShrimpDirector.VoltBubbleSpeed;
                        proj.ai[1] = 1f;
                        proj.ai[2] = MathHelper.Clamp(dist / SeaShrimpDirector.VoltBubbleSpeed, 10f, 88f);
                        proj.netUpdate = true;
                        break;
                    }
                }
            }

            //合击持位：双螯并拢在钳口，反坐回落
            for (int a = 0; a < 2; a++) {
                ctx.Claws[a] = new ClawDirective {
                    Mode = ClawMode.Strike,
                    Target = muzzle,
                    Spring = 0.5f,
                    Damping = 0.82f,
                    ClawOpen = 0f,
                };
            }
            ctx.SpineCurl = 0.18f * (1f - (t - SlamFrame) / (float)(Total - SlamFrame));
            ctx.AfterimageStrength = MathF.Max(ctx.AfterimageStrength, 0.45f);

            if (t >= Total) {
                return EndAttack(ctx, 55);
            }
            return null;
        }
    }
}
