using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States
{
    /// <summary>诅咒仪式登场：黑暗汇聚→尸骨凝聚→死寂→点睛→双手凝聚</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.Intro, typeof(SkeletronStateContext))]
    internal class SkeletronIntroState : SkeletronStateBase
    {
        public override string StateName => "Intro";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.Intro;

        internal const int GatherStart = 24;    //尸骨开始汇聚
        internal const int GatherCut = 118;     //汇聚硬切（尖啸前的吸气）
        internal const int SilenceEnd = 148;    //死寂结束
        internal const int EyeIgnite = 150;     //点睛
        internal const int HandSpawn = 168;     //双手凝聚
        internal const int IntroEnd = 238;

        public override void OnEnter(SkeletronStateContext context) {
            base.OnEnter(context);
            context.EyeFlame = 0f;
        }

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;

            if (Timer == 0) {
                npc.ai[SkeletronAiSlots.HeadPhase] = SkeletronPhase.Intro;
                npc.life = System.Math.Max(npc.life, 1);
                npc.Center = target.Center + new Vector2(0f, -390f);
                npc.alpha = 255;
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
            }

            npc.damage = 0;
            npc.dontTakeDamage = true;
            npc.velocity = Vector2.Zero;
            SettleRotation(npc, 0.2f);

            //黑暗领域随仪式压近
            float domain = MathHelper.Clamp(Timer / 60f, 0f, 0.55f);
            if (Timer > EyeIgnite) {
                domain = MathHelper.Lerp(0.55f, 0.16f, (Timer - EyeIgnite) / (float)(IntroEnd - EyeIgnite));
            }
            SkeletronScreenEffects.RequestDomain(domain);

            UpdateBeats(context, npc, target);

            Timer++;
            if (Timer > IntroEnd) {
                npc.dontTakeDamage = false;
                npc.damage = npc.defDamage;
                npc.ai[SkeletronAiSlots.HeadPhase] = SkeletronPhase.Bound;
                if (!VaultUtils.isClient) {
                    return new SkeletronHubState();
                }
            }
            return null;
        }

        private void UpdateBeats(SkeletronStateContext context, NPC npc, Player target) {
            //丧钟三响
            if (!VaultUtils.isServer && (Timer == 4 || Timer == 70 || Timer == EyeIgnite)) {
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 1f, Pitch = -0.82f }, npc.Center);
            }

            //尸骨与幽火汇聚（密度∝sqrt，72%硬切）
            if (!VaultUtils.isServer && Timer >= GatherStart && Timer < GatherCut) {
                float progress = (Timer - GatherStart) / (float)(GatherCut - GatherStart);
                float density = (float)System.Math.Sqrt(progress);
                if (Main.rand.NextFloat() < density * 0.85f) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2CircularEdge(320f, 320f) * Main.rand.NextFloat(0.55f, 1f);
                    PRTLoader.NewParticle<PRT_SkeleBoneChip>(pos, (npc.Center - pos) * 0.065f,
                        Color.White, Main.rand.NextFloat(0.7f, 1.2f))?.Configure(34, 0f);
                }
                if (Main.rand.NextFloat() < density * 0.7f) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2CircularEdge(260f, 260f) * Main.rand.NextFloat(0.6f, 1f);
                    //切向注入涡旋感
                    Vector2 pull = (npc.Center - pos) * 0.075f;
                    PRTLoader.NewParticle<PRT_SkeleGhostFlame>(pos, pull.RotatedBy(0.6f),
                        SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(1.1f, 1.9f))?.Configure(30, 0f);
                }
            }

            //头颅自黑暗中显形
            if (Timer >= GatherStart && Timer <= GatherCut) {
                int fade = (int)MathHelper.Lerp(255f, 30f, (Timer - GatherStart) / (float)(GatherCut - GatherStart));
                npc.alpha = fade;
            }
            //死寂：一切静止，只剩残余显形
            if (Timer > GatherCut && Timer <= SilenceEnd) {
                npc.alpha = System.Math.Max(npc.alpha - 4, 0);
            }

            //点睛：眼窝燃起，尖啸
            if (Timer == EyeIgnite) {
                npc.alpha = 0;
                context.EyeFlame = 1.6f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.1f, Pitch = -0.35f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Volume = 0.6f, Pitch = -0.7f }, npc.Center);
                    SkeletronScreenEffects.PushShockRing(npc.Center, 0.9f, 620f, 26);
                    SkeletronScreenEffects.PushShake(npc.Center, 8f);
                    for (int i = 0; i < 26; i++) {
                        PRTLoader.NewParticle<PRT_SkeleGhostFlame>(npc.Center + Main.rand.NextVector2Circular(40f, 40f),
                            Main.rand.NextVector2CircularEdge(6f, 6f),
                            SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(1.4f, 2.4f))?.Configure(Main.rand.Next(24, 40));
                    }
                }
            }

            //眼火回落至常燃
            if (Timer > EyeIgnite) {
                context.EyeFlame = MathHelper.Lerp(context.EyeFlame, 1f, 0.05f);
            }

            //双手自两侧凝聚
            if (Timer == HandSpawn) {
                if (!VaultUtils.isClient) {
                    context.Owner.SpawnHands();
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.8f, Pitch = -0.5f }, npc.Center);
                    for (int side = -1; side <= 1; side += 2) {
                        Vector2 handPos = npc.Center + new Vector2(side * 200f, 180f);
                        for (int i = 0; i < 12; i++) {
                            PRTLoader.NewParticle<PRT_SkeleGhostFlame>(handPos + Main.rand.NextVector2Circular(36f, 36f),
                                -Vector2.UnitY * Main.rand.NextFloat(1f, 3f),
                                SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(1.2f, 2f))?.Configure(Main.rand.Next(22, 36));
                        }
                    }
                }
            }

            //登场全程压迫性威压凝视（缓慢逼近悬点）
            Vector2 toPoint = target.Center + new Vector2(0f, -330f);
            npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.02f);
        }
    }
}
