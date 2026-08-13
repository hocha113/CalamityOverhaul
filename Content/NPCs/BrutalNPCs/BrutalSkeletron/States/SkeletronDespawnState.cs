using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States
{
    /// <summary>脱战消散：无有效目标，头颅升空散作幽火退场</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.Despawn, typeof(SkeletronStateContext))]
    internal class SkeletronDespawnState : SkeletronStateBase
    {
        public override string StateName => "Despawn";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.Despawn;

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            npc.dontTakeDamage = true;

            //升空减速漂离
            npc.velocity.X *= 0.96f;
            npc.velocity.Y -= 0.14f;
            if (npc.velocity.Y < -9f) {
                npc.velocity.Y = -9f;
            }
            SettleRotation(npc, 0.1f);

            //形体散作幽火
            npc.alpha = Math.Min(npc.alpha + 4, 255);
            context.EyeFlame = MathHelper.Clamp(1f - Timer / 40f, 0f, 1f);
            if (!VaultUtils.isServer && Timer % 3 == 0 && npc.alpha < 250) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(npc.Center + Main.rand.NextVector2Circular(40f, 40f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-3f, -1.4f)),
                    SkeletronRenderHelper.GhostDeep, Main.rand.NextFloat(1.3f, 2.2f))?.Configure(Main.rand.Next(24, 40));
            }

            if (Timer == 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.8f, Pitch = -1f }, npc.Center);
            }

            if (npc.timeLeft > 60) {
                npc.timeLeft = 60;
            }

            Timer++;
            //目标复活/回场则重新参战
            if (!VaultUtils.isClient && Timer > 30 && context.Target.Alives()
                && npc.Center.Distance(context.Target.Center) < 2200f && !context.Target.dead) {
                npc.alpha = 0;
                npc.dontTakeDamage = false;
                npc.timeLeft = 3600;
                return new SkeletronHubState();
            }
            return null;
        }
    }
}
