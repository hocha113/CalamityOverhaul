using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>50% 转阶段：清弹幕→中场嘶吼→三相裂解波→分身扩编</summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.PhaseTransition, typeof(CultistStateContext))]
    internal class CultistPhaseTransitionState : CultistStateBase
    {
        public override string StateName => "PhaseTransition";
        public override CultistStateIndex StateIndex => CultistStateIndex.PhaseTransition;

        private const int BlinkMoment = 12;
        private static readonly int[] ElementWaves = [44, 70, 96];
        private const int Duration = 152;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            context.Npc.dontTakeDamage = true;
            if (!VaultUtils.isClient) {
                //公平阀：清场再开戏
                CultistBossAI.ClearHostileProjectiles();
                CultistBossAI.DismissClones(context);
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            context.SkipDefaultHover = true;
            npc.velocity *= 0.9f;
            context.ElementAura = 1f;
            CultistScreenFX.DeclareVeil(npc.Center, 0.55f, context.Element);

            //中场就位
            if ((int)Timer == BlinkMoment && player.Alives()) {
                Vector2 target = player.Center + new Vector2(0f, -300f);
                if (!VaultUtils.isClient) {
                    CultistBossAI.BlinkTo(context, target);
                }
                else {
                    CultistRenderHelper.BlinkOut(npc.Center, context.Element);
                    CultistRenderHelper.BlinkIn(target, context.Element);
                }
            }

            //嘶吼定场
            if (Timer > BlinkMoment && Timer <= 40) {
                context.CastPose = CultistPose.Scream;
                context.CastGlow = (Timer - BlinkMoment) / 28f;
                if ((int)Timer == BlinkMoment + 8 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1.15f, Pitch = -0.15f }, npc.Center);
                }
            }

            //三相裂解波：火→冰→雷 逐波喷发
            for (int w = 0; w < ElementWaves.Length; w++) {
                if ((int)Timer == ElementWaves[w]) {
                    var element = (CultistElement)w;
                    CultistScreenFX.PushFlash(0.3f + w * 0.08f, 14);
                    CultistScreenFX.Punch(npc.Center, 5f + w, 12, "CultistPhase");
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.85f, Pitch = -0.1f + w * 0.15f }, npc.Center);
                        for (int i = 0; i < 16; i++) {
                            float angle = MathHelper.TwoPi * i / 16f + w * 0.35f;
                            Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 12f);
                            CultistRenderHelper.SpawnElementMote(npc.Center, vel, element,
                                Main.rand.NextFloat(0.9f, 1.5f), Main.rand.Next(22, 38));
                        }
                        for (int i = 0; i < 6; i++) {
                            PRTLoader.NewParticle<PRT_CultistShard>(npc.Center,
                                Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 9f),
                                CultistPalette.Main(element), Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(24, 40));
                        }
                    }
                    context.CastPose = CultistPose.Scream;
                    context.CastGlow = 1f;
                }
            }

            //扩编分身
            if ((int)Timer == 112 && !VaultUtils.isClient) {
                context.IsPhase2 = true;
                CultistBossAI.EnsureClones(context, 3);
            }

            if (Timer >= Duration) {
                context.PhaseTransitionDone = true;
                npc.dontTakeDamage = false;
                return new CultistWeaveState();
            }
            return null;
        }

        public override void OnExit(CultistStateContext context) {
            base.OnExit(context);
            context.PhaseTransitionDone = true;
            context.Npc.dontTakeDamage = false;
        }
    }
}
