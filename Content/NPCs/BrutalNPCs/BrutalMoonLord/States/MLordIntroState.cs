using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Events;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>日蚀降临：天幕遮蔽→剪影降下→双拍心跳→部件星光拼装→怒吼开战</summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.Intro, typeof(MLordContext))]
    internal class MLordIntroState : MLordStateBase
    {
        public override string StateName => "Intro";
        public override MLordStateIndex StateIndex => MLordStateIndex.Intro;

        internal const int DescentEnd = 90;
        internal const int HeartbeatEnd = 130;
        internal const int AssemblyTick = 130;
        internal const int RoarTick = 175;
        internal const int IntroEnd = 235;

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;

            if (Timer == 0) {
                npc.ai[MLordAiSlots.CorePhase] = MLordPhase.Intro;
                npc.Center = target.Center + new Vector2(0f, -1100f);
                npc.velocity = Vector2.Zero;
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie99 with { Volume = 0.9f, Pitch = -0.6f }, target.Center);
                }
            }

            npc.damage = 0;
            npc.dontTakeDamage = true;
            context.HoldAllParts = true;
            //日蚀吞噬天光
            context.EclipseDrive = MathHelper.Clamp(Timer / 80f, 0f, 1f);

            if (Timer < DescentEnd) {
                UpdateDescent(context);
            }
            else if (Timer < HeartbeatEnd) {
                UpdateHeartbeat(context);
            }
            else {
                UpdateAssembly(context);
            }

            Timer++;
            if (Timer > IntroEnd) {
                npc.dontTakeDamage = false;
                npc.ai[MLordAiSlots.CorePhase] = MLordPhase.Trinity;
                if (!VaultUtils.isClient) {
                    return NextAttack(context);
                }
            }
            return null;
        }

        /// <summary>剪影自日蚀中降下，星流向体内收拢</summary>
        private void UpdateDescent(MLordContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;

            //淡入：alpha 由 255 → 0
            npc.alpha = (int)MathHelper.Lerp(255f, 0f, VaultUtils.EaseInOutCubic(Timer / (float)DescentEnd));

            Vector2 toPoint = target.Center + new Vector2(0f, -420f);
            npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.045f);
            npc.velocity = Vector2.Zero;

            if (!VaultUtils.isServer) {
                MLordScreenFX.ConvergeStreak(npc.Center, 620f, Timer / (float)DescentEnd * 0.7f);
                MLordScreenEffects.PushGravityDim(npc.Center, Timer / (float)DescentEnd * 0.35f);
            }
        }

        /// <summary>威压来自静止：两拍心跳，天地无声</summary>
        private void UpdateHeartbeat(MLordContext context) {
            NPC npc = context.Npc;
            npc.alpha = 0;
            npc.velocity *= 0.9f;

            int t = Timer - DescentEnd;
            if ((t == 8 || t == 30) && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit57 with { Volume = 0.95f, Pitch = -0.85f }, npc.Center);
                MLordScreenFX.Punch(npc.Center, 3.5f, 9);
                context.HeartExposure = 0.3f;
            }
        }

        /// <summary>部件自星光拼装成形，怒吼定场</summary>
        private void UpdateAssembly(MLordContext context) {
            NPC npc = context.Npc;
            npc.alpha = 0;
            HoverTo(npc, context.Target.Center + new Vector2(0f, -420f), 4f, 0.03f);

            if (Timer == AssemblyTick) {
                if (!VaultUtils.isClient) {
                    context.Owner.SpawnParts();
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = -0.55f }, npc.Center);
                    MLordScreenFX.StarBurst(npc.Center, 1.6f, 26);
                }
            }

            if (Timer == RoarTick && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie92 with { Volume = 1.1f }, npc.Center);
                MoonlordDeathDrama.RequestLight(0.4f, npc.Center);
                MLordScreenEffects.PushStarRing(npc.Center, 1f, 860f, 34);
                MLordScreenFX.Punch(npc.Center, 9f, 22);
                VaultUtils.Text(MoonLordCoreAI.MoonLordIntro_Text.Value, MLordDirector.DeepViolet);
            }
            if (Timer > RoarTick && Timer < RoarTick + 22 && !VaultUtils.isServer) {
                MoonlordDeathDrama.RequestLight(0.4f * (1f - (Timer - RoarTick) / 22f), npc.Center);
            }
        }
    }
}
