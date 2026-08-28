using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>
    /// 破沙而出入场演出（~240f）：海床闷震蓄势（84f 预兆）→ 炸沙跃出 →
    /// 落定舒展 → 静止 60f 威压（威压=静止）→ 转向玩家开战。
    /// 全程无敌无伤，各端由 Timer 本地推演；出生摆位仅服务器写一次
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.Intro, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpIntroState : SeaShrimpStateBase
    {
        public override string StateName => "Intro";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.Intro;

        private const int BurstFrame = 84;
        private const int SettleFrame = 128;
        private const int StareEnd = 208;
        private const int Total = 240;

        public override void OnEnter(SeaShrimpStateContext ctx) {
            base.OnEnter(ctx);
            //出生摆位：埋进目标旁侧的海床之下（只服务器写，netUpdate 广播）
            if (!VaultUtils.isClient && ctx.Target != null && ctx.Target.active) {
                float side = ctx.Npc.Center.X >= ctx.Target.Center.X ? 1f : -1f;
                float burstX = ctx.Target.Center.X + side * 300f;
                float groundY = FindGroundY(new Vector2(burstX, ctx.Target.Center.Y - 160f));
                ctx.Npc.Center = new Vector2(burstX, groundY + 200f);
                ctx.Npc.velocity = Vector2.Zero;
                ctx.Npc.netUpdate = true;
            }
        }

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            ShrimpLocomotion loco = ctx.Owner.Locomotion;
            int t = (int)Timer;
            Timer++;

            npc.dontTakeDamage = true;
            //运镜窗口：仅前 20 帧敞开，重播不触发
            if (t < 20) {
                SeaShrimpCutscenes.ArmIntro(npc.Center);
            }
            SeaShrimpCutscenes.IntroAnchor = npc.Center;

            //各端一致的地表参考（同步位置 + 只读物块 = 同一结果）
            float surfaceY = FindGroundY(new Vector2(npc.Center.X, npc.Center.Y - 260f));
            Vector2 surface = new(npc.Center.X, surfaceY);

            if (t < BurstFrame) {
                //预兆段：埋着不可见，海床闷震、沙尘渐密、蓝光透缝
                loco.RequestScripted();
                npc.velocity = Vector2.Zero;
                ctx.BodyAlpha = 0f;
                float build = t / (float)BurstFrame;

                if (!Main.dedServ) {
                    if (t % 12 == 0) {
                        ShakeNearby(surface, 1.2f + build * 2.4f, 1100f);
                        SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.4f + build * 0.4f, Pitch = -0.6f, MaxInstances = 3 }, surface);
                    }
                    if (Main.rand.NextFloat() < 0.25f + build * 0.55f) {
                        Vector2 dustPos = surface + new Vector2(Main.rand.NextFloat(-80f, 80f), Main.rand.NextFloat(-4f, 4f));
                        PRTLoader.NewParticle<PRT_Smoke>(dustPos,
                            new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.4f, 1.4f) * (0.4f + build)),
                            new Color(150, 138, 110), Main.rand.NextFloat(0.4f, 0.8f))?.Configure(Main.rand.Next(22, 40), 0.55f);
                    }
                    //蓝光透缝：越接近爆发越亮
                    Lighting.AddLight(surface, 0.1f * build, 0.2f * build, 0.4f * build);
                    if (build > 0.6f && Main.rand.NextFloat() < 0.3f) {
                        PRTLoader.NewParticle<PRT_Spark>(surface + new Vector2(Main.rand.NextFloat(-56f, 56f), 0f),
                            new Vector2(0f, -Main.rand.NextFloat(1f, 3f)),
                            SeaShrimpRenderer.CrystalBlue, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(false, Main.rand.Next(10, 18));
                    }
                }
                return null;
            }

            if (t == BurstFrame) {
                //炸沙跃出：一帧位移到地表 + 上抛，怒吼与沙暴同拍
                loco.RequestScripted();
                npc.Center = surface + new Vector2(0f, -30f);
                npc.velocity = new Vector2(0f, -15f);
                ctx.BodyAlpha = 1f;
                ctx.SpineCurl = -0.8f;
                ctx.CrystalGlow = 1f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1f, Pitch = 0.15f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.9f, Pitch = -0.3f }, npc.Center);
                    ShakeNearby(npc.Center, 9f);
                    for (int i = 0; i < 34; i++) {
                        PRTLoader.NewParticle<PRT_Smoke>(surface + new Vector2(Main.rand.NextFloat(-60f, 60f), 0f),
                            new Vector2(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(2f, 9f)),
                            new Color(150, 138, 110), Main.rand.NextFloat(0.6f, 1.2f))?.Configure(Main.rand.Next(30, 55), 0.7f);
                    }
                    for (int i = 0; i < 18; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(surface,
                            new Vector2(Main.rand.NextFloat(-5f, 5f), -Main.rand.NextFloat(3f, 10f)),
                            Color.Lerp(SeaShrimpRenderer.CrystalBlue, Color.White, Main.rand.NextFloat(0.5f)),
                            Main.rand.NextFloat(0.5f, 0.9f))?.Configure(true, Main.rand.Next(16, 28));
                    }
                }
                return null;
            }

            if (t < SettleFrame) {
                //腾跃段：升势渐缓，蜷曲舒展
                loco.RequestScripted();
                npc.velocity *= 0.9f;
                npc.velocity.Y += 0.22f;
                float unfold = (t - BurstFrame) / (float)(SettleFrame - BurstFrame);
                ctx.SpineCurl = -0.8f * (1f - unfold * unfold);
                ctx.TailFlare = unfold;
                ctx.BodyAlpha = 1f;
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, 0.8f);
                return null;
            }

            if (t < StareEnd) {
                //威压静止：落回地面驻停，晶光脉冲蓄势，触角尝水
                HoldInPlace(ctx);
                float stare = (t - SettleFrame) / (float)(StareEnd - SettleFrame);
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, 0.35f + stare * 0.6f);
                ctx.TailFlare = 0.5f;
                if ((t == SettleFrame + 24 || t == SettleFrame + 52) && !Main.dedServ) {
                    ctx.Owner.Skeleton.Antennae[0].Nudge(new Vector2(2.4f, -1.2f));
                    ctx.Owner.Skeleton.Antennae[1].Nudge(new Vector2(-2f, -1f));
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.4f, MaxInstances = 2 }, npc.Center);
                }
                //钳口开合示威
                float snap = MathF.Sin(stare * MathHelper.Pi * 3f);
                ClawDirective guard = ClawDirective.GuardDefault;
                guard.ClawOpen = MathF.Max(snap, 0f);
                ctx.Claws[0] = guard;
                ctx.Claws[1] = guard;
                return null;
            }

            //转向玩家：低速蹭向目标，朝向自然转过去
            ctx.Owner.Locomotion.RequestCrawlTo(ctx.Target.Center, 0.12f);
            ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, 0.9f);

            if (t >= Total) {
                ctx.AttackCooldown = 40;
                return new SeaShrimpHubState();
            }
            return null;
        }
    }
}
