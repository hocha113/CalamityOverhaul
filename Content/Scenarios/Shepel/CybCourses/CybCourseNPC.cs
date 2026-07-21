using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    //禁刷怪，白名单仅SantaNK1
    internal class CybCourseNPC : GlobalNPC
    {
        public static readonly HashSet<int> SpawnWhitelist = [NPCID.SantaNK1];

        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) {
            if (!CybCourseWorld.Active)
                return;
            spawnRate = 0;
            maxSpawns = 0;
        }

        public override bool PreAI(NPC npc) {
            if (!CybCourseWorld.Active)
                return true;
            if (SpawnWhitelist.Contains(npc.type))
                return true;
            npc.active = false;
            npc.netUpdate = true;
            return false;
        }
    }
}
