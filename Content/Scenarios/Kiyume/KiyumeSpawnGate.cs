using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume
{
    //自然刷怪双闸：本轮只做场景，梦里不该有原版史莱姆游荡
    //NormalUpdates=false 不停 NPC.SpawnNPC，所以要在 GlobalNPC 层硬关（镜像 OldNetSpawnGate）
    internal class KiyumeSpawnGate : GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) {
            if (!KiyumeWorld.Active) {
                return;
            }
            spawnRate = int.MaxValue;
            maxSpawns = 0;
        }

        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo) {
            if (!KiyumeWorld.Active) {
                return;
            }
            pool.Clear();
        }
    }
}
