using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States
{
    /// <summary>脱战：转身走进暴雪深处，身形被雪幕吞没</summary>
    [InnoVault.StateMachines.VaultState((int)DeerclopsStateIndex.Despawn, typeof(DeerclopsStateContext))]
    internal class DeerclopsDespawnState : DeerclopsStateBase
    {
        public override string StateName => "Despawn";
        public override DeerclopsStateIndex StateIndex => DeerclopsStateIndex.Despawn;

        private const int DissolveStart = 60;
        private const int StateEnd = 175;

        public override void OnEnter(DeerclopsStateContext context) {
            base.OnEnter(context);
            DeerclopsAI.ClearHostileProjectiles();
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.DeerclopsScream with { Volume = 0.6f, Pitch = -0.7f }, context.Npc.Center);
            }
        }

        public override IDeerclopsState OnUpdate(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            npc.dontTakeDamage = true;
            npc.damage = 0;
            //背身而去
            context.TargetXOverride = npc.Center.X + npc.direction * 900f;
            context.MoveSpeedMult = 0.85f;
            context.VeilTarget = MathHelper.Clamp(1f - Timer / 150f, 0f, 1f) * 0.4f;

            if (Timer > DissolveStart) {
                context.Dissolve = MathHelper.Clamp((Timer - DissolveStart) / 100f, 0f, 1f);
                //身形化雪(本端)
                if (!Main.dedServ && Timer % 3 == 0) {
                    Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Snow,
                        0f, -Main.rand.NextFloat(1f, 3f), 100, default, Main.rand.NextFloat(1.2f, 2f));
                    dust.noGravity = true;
                }
            }

            if (Timer > StateEnd && !VaultUtils.isClient) {
                npc.active = false;
                npc.netUpdate = true;
            }
            return null;
        }
    }
}
