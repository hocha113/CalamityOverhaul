using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>合相被拆台的失衡硬直:浑天仪抖乱,身位下坠,受伤加深(拆台奖励)</summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Stagger, typeof(CultistStateContext))]
    internal class CultistStaggerState : CultistStateBase
    {
        public override string StateName => "CultistStagger";
        public override CultistStateIndex StateIndex => CultistStateIndex.Stagger;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            context.StaggerWobble = 1f;
            npc.velocity = new Vector2(0f, 2.6f);
            CultistMotion.Shake(npc.Center, 7f, 14);
            CultistScreenFX.PushFlash(0.3f);
            CultistMotion.RuneBurst(npc.Center, CultistMotion.RuneGold, 18, 8f);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.9f, Pitch = 0.1f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Zombie103 with { Volume = 1f, Pitch = 0.25f }, npc.Center);
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            SetPose(npc, 0);
            npc.velocity *= 0.94f;
            context.StaggerWobble = MathHelper.Max(context.StaggerWobble,
                MathHelper.Clamp(1f - Timer / (float)context.StaggerDuration, 0f, 1f));

            if (Timer % 9 == 0) {
                CultistMotion.RuneBurst(npc.Center + Main.rand.NextVector2Circular(30f, 40f),
                    CultistMotion.PhaseCore(context.Phase), 2, 3f);
            }

            if (VaultUtils.isClient) {
                return null;
            }
            if (Timer >= context.StaggerDuration) {
                return new CultistCoilState(24);
            }
            return null;
        }
    }
}
