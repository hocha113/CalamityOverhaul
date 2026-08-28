using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiame
{
    //自然刷怪双闸：雨里只有导演生成的伞鬼，不该有原版史莱姆游荡
    //NormalUpdates=false 不停 NPC.SpawnNPC，所以要在 GlobalNPC 层硬关（镜像 KiyumeSpawnGate）
    internal class KiameSpawnGate : GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) {
            if (!KiameWorld.Active) {
                return;
            }
            spawnRate = int.MaxValue;
            maxSpawns = 0;
        }

        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo) {
            if (!KiameWorld.Active) {
                return;
            }
            pool.Clear();
        }
    }
}
