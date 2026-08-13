using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>撤离：法阵自身合拢、吟唱降调、折入光门消失</summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Despawn, typeof(CultistStateContext))]
    internal class CultistDespawnState : CultistStateBase
    {
        public override string StateName => "Despawn";
        public override CultistStateIndex StateIndex => CultistStateIndex.Despawn;

        private const int FoldEnd = 74;
        private const int Duration = 100;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            context.Npc.dontTakeDamage = true;
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            context.SkipDefaultHover = true;
            npc.velocity = new Vector2(0f, -0.6f);
            context.CastPose = CultistPose.CastUp;
            CultistScreenFX.DeclareVeil(npc.Center, 0.35f * (1f - Timer / (float)Duration), context.Element);

            //身前法阵反向合拢
            context.StageSigilPos = npc.Center;
            context.StageSigilRadius = 150f;
            context.StageSigilProgress = MathHelper.Clamp(1f - Timer / (float)FoldEnd, 0f, 1f);

            //渐隐+降调吟唱
            npc.alpha = (int)MathHelper.Clamp(Timer / (float)FoldEnd * 255f, 0f, 255f);
            if ((int)Timer % 22 == 0 && Timer < FoldEnd && !VaultUtils.isServer) {
                CultistRenderHelper.ChantVoice(npc.Center, 0.6f, MathHelper.Lerp(0f, -0.6f, Timer / (float)FoldEnd));
            }
            if (!VaultUtils.isServer && Main.rand.NextBool(3) && Timer < FoldEnd) {
                CultistRenderHelper.ConvergeRunes(npc.Center, 220f, context.Element, 0.7f);
            }

            //合拢眨闪
            if ((int)Timer == FoldEnd) {
                CultistScreenFX.PushFlash(0.3f, 12);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.9f, Pitch = -0.4f }, npc.Center);
                }
            }

            if (Timer >= Duration && !VaultUtils.isClient) {
                CultistBossAI.DismissClones(context);
                CultistBossAI.CleanupMinions(includeDragons: false);
                npc.active = false;
                npc.netUpdate = true;
            }
            return null;
        }
    }
}
