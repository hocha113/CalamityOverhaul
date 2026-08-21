using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>无目标撤离：召物随散，符文化雾升离</summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Despawn, typeof(CultistStateContext))]
    internal class CultistDespawnState : CultistStateBase
    {
        public override string StateName => "CultistDespawn";
        public override CultistStateIndex StateIndex => CultistStateIndex.Despawn;

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            SetPose(npc, 13);
            npc.velocity = new Vector2(0f, -MathHelper.Clamp(Timer * 0.08f, 0f, 7f));
            npc.alpha = (int)MathHelper.Clamp(Timer * 4.2f, 0f, 255f);
            npc.dontTakeDamage = true;

            if (Timer % 5 == 0) {
                CultistMotion.RuneBurst(npc.Center, CultistMotion.PaleClone, 1, 3f);
            }

            //清场随从与弹幕（权威端一次）
            if (Timer == 10 && !VaultUtils.isClient) {
                CultistBossAI.ClearMinionsAndProjectiles(npc);
            }

            if (VaultUtils.isClient) {
                return null;
            }
            if (Timer >= 70) {
                npc.life = 0;
                npc.HitEffect();
                npc.active = false;
                if (Main.netMode == NetmodeID.Server) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                }
            }
            return null;
        }
    }
}
