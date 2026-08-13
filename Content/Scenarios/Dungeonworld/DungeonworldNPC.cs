using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld
{
    //守卫治理第一层(§4.5/R3):子世界内不无条件刷地牢守卫
    internal class DungeonworldNPC : GlobalNPC
    {
        //未败骷髅王时原版地牢分支把每次自然刷怪替换为守卫68且spawnRate=10(TML NPC.cs L75817/L73961)
        //pool[0]=原版选择的整体权重,清零即屏蔽该分支;已败后原版怪表照常生效
        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo) {
            if (!Dungeonworld.Active) {
                return;
            }
            if (!NPC.downedBoss3 && spawnInfo.Player.ZoneDungeon) {
                pool[0] = 0f;
            }
        }
    }
}
