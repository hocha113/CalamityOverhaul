using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>离场：她收起光辉，向上飘散成光雨——去时也要好看</summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.Despawn, typeof(EmpressStateContext))]
    internal class EmpressDespawnState : EmpressStateBase
    {
        public override string StateName => "EmpressDespawn";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.Despawn;

        private const int TotalTime = 110;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            context.Npc.damage = 0;
            PlayLocal(SoundID.Item165 with { Volume = 0.9f }, context.Npc.Center);
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            npc.damage = 0;
            //缓升离场
            npc.velocity = Vector2.Lerp(npc.velocity, new Vector2(0f, -6.5f), 0.045f);
            npc.Opacity = MathHelper.Clamp(1f - Timer / (TotalTime - 20f), 0f, 1f);

            context.Pose = EmpressPose.Idle;
            context.PoseTimer = 0f;

            //身形散成上升的光羽
            if (!VaultUtils.isServer && Timer % 2 == 0) {
                float hue = Main.rand.NextFloat();
                PRTLoader.NewParticle<PRT_EmpressPetalDust>(npc.Center + Main.rand.NextVector2Circular(70f, 90f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-2.6f, -1.2f)),
                    EmpressMotion.Prism(hue, 0.62f), Main.rand.NextFloat(0.5f, 0.95f))?.Configure(44, hue);
            }

            if (Timer >= TotalTime && !VaultUtils.isClient) {
                npc.active = false;
                npc.netUpdate = true;
            }
            return null;
        }
    }
}
