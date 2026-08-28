using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>
    /// 渊喉水炮（P2+，蓄力口吐巨型水柱）：转身对线 → 70f 蓄力
    /// （头后仰反向蓄势、口部向心水滴流 ∝√charge、75% 截停静默、细流预览瞄准线）
    /// → f52 锁向锁扫向（承诺，此后不再追瞄）→ 点火白闪 ≤2f → 96f 巨柱喷射
    /// （慢扫 JetSweepRate 声明式、本体持续后坐、低频 rumble）→ 塌缩断流 → 收势。
    /// 扫向 = 锁定帧玩家在柱线哪一侧（确定性输入，各端一致，真判定走弹幕同步 ai）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.AbyssJet, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpAbyssJetState : SeaShrimpStateBase
    {
        public override string StateName => "AbyssJet";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.AbyssJet;

        private const int ChargeEnd = 70;
        private const int LockFrame = 52;
        private const int QuietFrame = 53;
        private static int FireEnd => ChargeEnd + 12 + SeaShrimpDirector.JetFireFrames + 14;
        private static int Total => FireEnd + 20;

        private float lockAngle;
        private float sweepDir;
        private bool angleInit;

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            Timer++;

            Vector2 mouth = npc.Center + lockAngle.ToRotationVector2() * 52f;

            if (t < ChargeEnd) {
                float w = t / (float)ChargeEnd;
                if (t < LockFrame) {
                    //追瞄段：柱线跟踪目标
                    Vector2 to = PredictTarget(ctx, 8f) - npc.Center;
                    lockAngle = to.ToRotation();
                    angleInit = true;
                }
                if (t == LockFrame) {
                    //锁向即承诺：扫向=此刻目标相对柱线的偏侧（确定性输入）
                    Vector2 rel = ctx.Target.Center - npc.Center;
                    float side = MathHelper.WrapAngle(rel.ToRotation() - lockAngle);
                    sweepDir = side >= 0f ? 1f : -1f;
                }
                //转身对线：水炮必须正对出流方向
                HoldFacing(ctx, lockAngle, MathHelper.Lerp(0.09f, 0.025f, w));

                //头后仰反向蓄势：越满仰得越狠（口吐前的吸气）
                float rear = MathF.Pow(w, 5f);
                ctx.SpineCurl = -0.42f * rear;
                ctx.TailFlare = 0.3f + 0.3f * w;
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, w);
                ctx.WaveGain = 0.25f;

                //口部向心水滴流：密度 ∝√charge，75% 截停静默（吸气拍）
                bool quiet = t >= QuietFrame;
                if (!Main.dedServ && !quiet && angleInit
                    && Main.rand.NextFloat() < 0.30f + 0.6f * MathF.Sqrt(w)) {
                    Vector2 from = mouth + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(60f, 150f);
                    PRTLoader.NewParticle<PRT_AbyssGlob>(from, (mouth - from) * 0.09f,
                        Color.Lerp(SeaShrimpVFX.Deep, SeaShrimpVFX.Body, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.26f, 0.44f))?.Configure(14, 1.8f);
                }
                if (t == 6 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 2 }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.5f, Pitch = -0.45f, MaxInstances = 2 }, npc.Center);
                }

                //细流预览瞄准线：锁定后转实热
                float aimAlpha = MathHelper.Clamp((t - 16) / 24f, 0f, 1f) * (t >= LockFrame ? 0.7f : 0.32f);
                if (angleInit) {
                    ctx.AddSolidBeam(mouth, lockAngle.ToRotationVector2(),
                        SeaShrimpDirector.JetMaxLength * 0.6f, aimAlpha, t >= LockFrame ? 1f : 0.55f);
                }
                return null;
            }

            if (t == ChargeEnd) {
                //点火帧：白闪 ≤2f（水不是能量，白只住在这一拍）+ 后坐踢
                npc.velocity -= lockAngle.ToRotationVector2() * 4.2f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item92 with { Volume = 1f, Pitch = -0.35f }, mouth);
                    SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.95f, Pitch = -0.15f }, mouth);
                    ShakeNearby(npc.Center, 7f);
                    ctx.AddRing(mouth, 190f, 20, 1f);
                    EverdeepVFX.SplashBurst(mouth, -lockAngle.ToRotationVector2() * 12f, 1.2f);
                    for (int i = 0; i < 6; i++) {
                        PRTLoader.NewParticle<PRT_AbyssSpark>(mouth,
                            Main.rand.NextVector2Circular(5f, 5f), SeaShrimpVFX.Foam,
                            Main.rand.NextFloat(0.9f, 1.3f))?.Configure(12);
                    }
                }
                if (!VaultUtils.isClient) {
                    int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.JetDamage);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), mouth, Vector2.Zero,
                        ModContent.ProjectileType<SeaShrimpJetBeam>(), damage, 4f, Main.myPlayer,
                        npc.whoAmI, lockAngle, SeaShrimpDirector.JetSweepRate * sweepDir);
                }
            }

            if (t > ChargeEnd && t < FireEnd) {
                //喷射段：身轴跟扫、持续后坐（发射器反坐是质量感）、低频 rumble
                int fireAge = t - ChargeEnd;
                float curAngle = lockAngle + SeaShrimpDirector.JetSweepRate * sweepDir * fireAge;
                HoldFacing(ctx, curAngle, 0.08f);
                npc.velocity -= curAngle.ToRotationVector2() * 0.06f;
                ctx.SpineCurl = 0.14f;
                ctx.TailFlare = 0.85f;
                ctx.CrystalGlow = 1f;
                if (!Main.dedServ && t % 10 == 0) {
                    ShakeNearby(npc.Center, 1.4f);
                }
                return null;
            }

            HoldInPlace(ctx);
            if (t >= Total) {
                return EndAttack(ctx, 64);
            }
            return null;
        }
    }
}
