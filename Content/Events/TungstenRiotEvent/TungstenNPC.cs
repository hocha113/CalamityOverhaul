using CalamityMod.NPCs.NormalNPCs;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Events.TungstenRiotEvent
{
    internal class TungstenNPC : GlobalNPC
    {
        public override void SetDefaults(NPC npc) {
            TungstenRiot.SetEventNPC(npc);
        }

        public override bool PreAI(NPC npc) {
            bool? tungstenset = TungstenRiot.Instance.UpdateNPCPreAISet(npc);
            return tungstenset ?? base.PreAI(npc);
        }

        public override void OnKill(NPC npc) {
            if (TungstenRiot.Instance.TungstenRiotIsOngoing) {
                TungstenRiot.Instance.TungstenKillNPC(npc);
            }
        }

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot) {
            TungstenRiot.Instance.ModifyEventNPCLoot(npc, ref npcLoot);
        }

        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) {
            if (TungstenRiot.Instance.TungstenRiotIsOngoing) {
                maxSpawns = (int)(maxSpawns * 1.15f);
                spawnRate = 2;
            }
        }

        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo) {
            if (TungstenRiot.Instance.TungstenRiotIsOngoing) {
                pool.Clear();
                foreach (int type in TungstenRiot.TungstenEventNPCDic.Keys) {
                    if (!pool.ContainsKey(type)) {
                        pool.Add(type, TungstenRiot.TungstenEventNPCDic[type].SpawnRate);
                    }
                }
                if (TungstenRiot.Instance.EventKillRatio < 0.5f) {
                    pool.Add(ModContent.NPCType<WulfrumAmplifier>(), 0.25f);
                }
            }
        }
    }
}
