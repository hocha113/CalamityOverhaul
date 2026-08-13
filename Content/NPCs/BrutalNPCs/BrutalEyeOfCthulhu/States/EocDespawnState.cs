using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.States
{
    /// <summary>撤离：无有效目标或天亮，加速升空化雾散去</summary>
    [InnoVault.StateMachines.VaultState((int)EocStateIndex.Despawn, typeof(EocStateContext))]
    internal class EocDespawnState : EocStateBase
    {
        public override string StateName => "EocDespawn";
        public override EocStateIndex StateIndex => EocStateIndex.Despawn;
        public override bool AllowFogStep => false;

        private const int DespawnTime = 100;

        public override void OnEnter(EocStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            npc.damage = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie3 with { Volume = 0.9f, Pitch = -0.6f }, npc.Center);
            }
        }

        public override IEocState OnUpdate(EocStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            npc.dontTakeDamage = true;

            //加速升空，化雾
            npc.velocity.Y -= 0.32f;
            npc.velocity.X *= 0.98f;
            npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;
            float progress = Timer / (float)DespawnTime;
            context.FogHideGoal = progress * 1.2f;

            if (Timer % 4 == 0) {
                EocMotion.MistPuff(npc.Center, 1, 1.1f, 0.42f);
            }

            npc.EncourageDespawn(10);

            Timer++;
            if (Timer >= DespawnTime && !VaultUtils.isClient) {
                npc.active = false;
                npc.netUpdate = true;
            }

            return null;
        }
    }
}
