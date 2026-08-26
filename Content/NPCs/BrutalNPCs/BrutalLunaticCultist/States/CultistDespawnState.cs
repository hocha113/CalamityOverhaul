using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>无目标撤离:诸星随散,黄道环收拢,符文化雾升离</summary>
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
            context.OrreryReveal = MathHelper.Clamp(3f - Timer * 0.06f, 0f, 3f);

            if (Timer % 5 == 0) {
                CultistMotion.RuneBurst(npc.Center, CultistMotion.RuneGold, 1, 3f);
            }

            //清场(权威端一次):星球退场,黄道环收拢
            if (Timer == 10 && !VaultUtils.isClient) {
                CultistBossAI.ClearHostileKit(npc);
                CultistPlanetProj.BeginDeparture(npc.whoAmI);
                CultistZodiacRing.BeginCollapse(npc.whoAmI);
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
