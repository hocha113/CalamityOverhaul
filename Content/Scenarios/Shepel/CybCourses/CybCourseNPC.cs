using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    //教程世界NPC生成管控，完全禁止自然刷怪，仅允许白名单内的手动生成NPC存活
    internal class CybCourseNPC : GlobalNPC
    {
        //教程中允许存在的NPC白名单，仅包含流程中主动生成的圣诞NK1
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
