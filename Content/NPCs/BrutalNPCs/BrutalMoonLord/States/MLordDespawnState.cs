using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Events;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>月退：无有效目标时收拢部件升空遁入日蚀，随光而逝</summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.Despawn, typeof(MLordContext))]
    internal class MLordDespawnState : MLordStateBase
    {
        public override string StateName => "Despawn";
        public override MLordStateIndex StateIndex => MLordStateIndex.Despawn;

        internal const int PartsGoneTick = 78;
        internal const int LeaveEnd = 104;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            context.Npc.ai[MLordAiSlots.CorePhase] = MLordPhase.Leaving;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 0.9f, Pitch = -0.5f }, context.Npc.Center);
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;
            npc.dontTakeDamage = true;
            npc.damage = 0;
            context.HoldAllParts = true;
            context.EclipseDrive = MathHelper.Clamp(1f - Timer / (float)LeaveEnd, 0f, 1f);

            //复合加速升空
            npc.velocity.X *= 0.92f;
            npc.velocity.Y -= 0.42f;
            if (npc.velocity.Y < -34f) {
                npc.velocity.Y = -34f;
            }

            if (Timer > 30 && !VaultUtils.isServer) {
                MoonlordDeathDrama.RequestLight(MathHelper.Clamp((Timer - 30) / 50f, 0f, 0.45f), npc.Center);
                if (Main.rand.NextBool(2)) {
                    MLordScreenFX.StarBurst(npc.Center + Main.rand.NextVector2Circular(120f, 200f), 0.4f, 3);
                }
            }

            if (Timer == PartsGoneTick && !VaultUtils.isClient) {
                context.Owner.RemoveAllServants(despawnEffect: true);
            }

            Timer++;
            if (Timer >= LeaveEnd && !VaultUtils.isClient) {
                //移除形制对齐原版 CheckActive：life 归零再失活，SyncNPC 收包端只认
                //life<=0 为摘除信号，带血失活会在联机端留下一具幽灵核心
                npc.life = 0;
                npc.active = false;
                npc.netUpdate = true;
            }
            return null;
        }
    }
}
