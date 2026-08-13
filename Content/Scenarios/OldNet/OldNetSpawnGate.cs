using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet
{
    //旧网全禁自然刷怪(M1-PLAN §2.0):NormalUpdates=false 只停时间与世界更新,不拦 NPC.SpawnNPC;
    //旧网固定黑夜且 worldSurface 压至地板下,玩法层判"地表",夜间怪具备刷出条件,须在此关死。
    //两个钩子都只作用于自然刷怪管线,手动 NPC.NewNPC(ICE/演出实体)不经它们,不受影响。
    internal class OldNetSpawnGate : GlobalNPC
    {
        //主闸:maxSpawns=0 使 nearbyActiveNPCs < maxSpawns 永假,整段自然刷怪跳过(TML NPC.cs L74086);
        //spawnRate 拉满兜底,后续 mod 钩子做比例缩放也无法复活
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) {
            if (!OldNetWorld.Active) {
                return;
            }
            spawnRate = int.MaxValue;
            maxSpawns = 0;
        }

        //双保险:清池后 ChooseSpawn 返回 null 直接终止本次刷怪(TML NPC.cs L74532),挡事件/Boss 注入的池项
        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo) {
            if (!OldNetWorld.Active) {
                return;
            }
            pool.Clear();
        }
    }
}
