using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Events;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 核心裸露转换：部件全破后的仪式拍——胸甲炸开、心跳加速、日蚀压满，
    /// 自此核心可被伤害，真眼集群成为常驻威胁
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.CoreExposure, typeof(MLordContext))]
    internal class MLordCoreExposureState : MLordStateBase
    {
        public override string StateName => "CoreExposure";
        public override MLordStateIndex StateIndex => MLordStateIndex.CoreExposure;

        internal const int CrackTick = 44;
        internal const int CeremonyEnd = 170;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                //转换即清场（公平阀门）
                foreach (Projectile p in Main.ActiveProjectiles) {
                    if (p.hostile) {
                        p.Kill();
                    }
                }
                context.Npc.TargetClosest();
                context.Npc.netUpdate = true;
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;

            context.HoldAllParts = true;
            context.EclipseDrive = MathHelper.Max(context.EclipseDrive, MathHelper.Clamp(Timer / 60f, 0f, 1f));
            HoverTo(npc, target.Center + new Vector2(0f, -380f), 4.5f, 0.04f);

            if (Timer < CrackTick) {
                //震颤蓄势
                context.SetChargeState(Timer / (float)CrackTick);
                if (!VaultUtils.isServer) {
                    MLordScreenFX.ConvergeStreak(npc.Center, 380f, Timer / (float)CrackTick);
                }
            }
            else {
                context.HeartExposure = 1f;
            }

            if (Timer == CrackTick) {
                //胸甲炸开
                context.Owner.PopChestPlates();
                if (!VaultUtils.isServer) {
                    MoonlordDeathDrama.RequestLight(0.7f, npc.Center);
                    MLordScreenEffects.PushStarRing(npc.Center, 1.1f, 900f, 36);
                    MLordScreenFX.StarBurst(npc.Center, 2f, 30);
                    MLordScreenFX.Punch(npc.Center, 12f, 22);
                    SoundEngine.PlaySound(SoundID.NPCDeath62 with { Volume = 1.1f, Pitch = -0.4f }, npc.Center);
                    VaultUtils.Text(MoonLordCoreAI.MoonLordCoreExposed_Text.Value, MLordDirector.Phantasmal);
                }
            }
            if (Timer > CrackTick && Timer < CrackTick + 24 && !VaultUtils.isServer) {
                MoonlordDeathDrama.RequestLight(0.7f * (1f - (Timer - CrackTick) / 24f), npc.Center);
            }

            //心跳三连加速
            if ((Timer == CrackTick + 30 || Timer == CrackTick + 54 || Timer == CrackTick + 72) && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit57 with { Volume = 1f, Pitch = -0.8f }, npc.Center);
                MLordScreenFX.Punch(npc.Center, 4f, 8);
            }

            Timer++;
            if (Timer >= CeremonyEnd) {
                npc.ai[MLordAiSlots.CorePhase] = MLordPhase.CoreExposed;
                if (!VaultUtils.isClient) {
                    return NextAttack(context);
                }
            }
            return null;
        }
    }
}
