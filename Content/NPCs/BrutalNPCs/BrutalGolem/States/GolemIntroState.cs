using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States
{
    /// <summary>祭坛启动登场：石壳苏醒 → 逐部件点火 → 双拳合体 → 头颅落位 → 震地宣告</summary>
    [InnoVault.StateMachines.VaultState((int)GolemStateIndex.Intro, typeof(GolemStateContext))]
    internal class GolemIntroState : GolemStateBase
    {
        public override string StateName => "Intro";
        public override GolemStateIndex StateIndex => GolemStateIndex.Intro;

        internal static int WakeEnd => 50;         //石壳苏醒（淡入+落尘）
        internal static int IgniteLeft => 62;      //左肩机关点火
        internal static int IgniteRight => 82;     //右肩机关点火
        internal static int IgniteGem => 102;      //太阳宝石点火
        internal static int FistSpawnTick => 120;  //双拳出土合体
        internal static int HeadSpawnTick => 158;  //头颅落位
        internal static int StompTick => 194;      //宣告重踏
        internal static int IntroEnd => 232;

        public override IGolemState OnUpdate(GolemStateContext context) {
            NPC npc = context.Npc;

            if (Timer == 0) {
                npc.ai[GolemAiSlots.BodyPhase] = GolemPhase.Intro;
                npc.TargetClosest();
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
            }

            npc.damage = 0;
            npc.dontTakeDamage = true;
            GroundBrake(npc);
            context.FrameMode = 0;

            //石壳苏醒：淡入 + 天顶落尘
            if (Timer < WakeEnd) {
                npc.alpha = System.Math.Max(npc.alpha - 6, 0);
                if (!VaultUtils.isServer && Timer % 6 == 0) {
                    Vector2 dustPos = npc.Center + new Vector2(Main.rand.NextFloat(-120f, 120f), -140f);
                    Dust dust = Dust.NewDustPerfect(dustPos, DustID.Stone, new Vector2(0, Main.rand.NextFloat(1f, 3f)), 80, default, 1.2f);
                    dust.noGravity = false;
                }
                if (!VaultUtils.isServer && Timer == 10) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Pitch = -0.8f, Volume = 1.1f }, npc.Center);
                }
            }
            else {
                npc.alpha = 0;
            }

            //逐部件点火
            if (!VaultUtils.isServer) {
                if (Timer == IgniteLeft || Timer == IgniteRight) {
                    Vector2 shoulder = npc.Center + new Vector2(Timer == IgniteLeft ? -80f : 74f, -9f);
                    SoundEngine.PlaySound(SoundID.Item93 with { Pitch = -0.2f, Volume = 0.6f }, shoulder);
                    for (int i = 0; i < 8; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(shoulder, VaultUtils.RandVr(1f, 4f),
                            new Color(255, 190, 80), Main.rand.NextFloat(0.8f, 1.2f)).Configure(true, 18);
                    }
                }
                if (Timer == IgniteGem) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.3f, Volume = 0.9f }, npc.Center);
                    for (int i = 0; i < 16; i++) {
                        PRTLoader.NewParticle<PRT_Light>(npc.Center + VaultUtils.RandVr(0f, 30f),
                            VaultUtils.RandVr(1f, 5f), new Color(255, 210, 110), Main.rand.Next(1, 2)).Configure(26);
                    }
                }
            }

            //宝石点火后脉络升温
            if (Timer > IgniteGem) {
                context.VeinGlow = MathHelper.Clamp((Timer - IgniteGem) / 60f, 0f, 1f) * 0.6f;
                context.SetChargeState(1, MathHelper.Clamp((Timer - IgniteGem) / (float)(IntroEnd - IgniteGem), 0f, 1f));
            }

            //双拳出土合体
            if (Timer == FistSpawnTick) {
                if (!VaultUtils.isClient) {
                    context.Owner.SpawnFists();
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.4f }, npc.Center);
                    GolemScreenEffects.Shake(3f);
                }
            }

            //头颅落位
            if (Timer == HeadSpawnTick) {
                if (!VaultUtils.isClient) {
                    context.Owner.SpawnAttachedHead();
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.6f, Volume = 1f }, npc.Center);
                }
            }

            //宣告重踏：短跳 + 落地宣言
            if (Timer == StompTick) {
                npc.velocity.Y = -7.5f;
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
            }
            if (Timer > StompTick && Timer < IntroEnd && npc.velocity.Y == 0f && Timer > StompTick + 8 && Counter == 0) {
                Counter = 1;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.4f }, npc.Center);
                    GolemScreenEffects.Shake(6f);
                    GolemScreenEffects.PushShockRing(npc.Bottom, 0.85f, 620f);
                }
            }

            Timer++;
            if (Timer > IntroEnd) {
                npc.dontTakeDamage = false;
                npc.ai[GolemAiSlots.BodyPhase] = GolemPhase.Armed;
                if (!VaultUtils.isClient) {
                    return new GolemConnectorState();
                }
            }
            return null;
        }
    }
}
