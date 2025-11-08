using CalamityOverhaul.Content.ADV.Common;
using Terraria;

namespace CalamityOverhaul.Content.ADV
{
    internal class ADVHook : ICWRLoader
    {
        void ICWRLoader.LoadData() {
            On_NPC.NPCLoot += OnNPCLootHook;
        }

        void ICWRLoader.UnLoadData() {
            On_NPC.NPCLoot -= OnNPCLootHook;
        }

        private void OnNPCLootHook(On_NPC.orig_NPCLoot orig, NPC npc) {
            orig.Invoke(npc);
            if (!VaultLoad.LoadenContent) {
                return;
            }
            if (!VaultUtils.isClient) {//仅客户端处理
                return;
            }
            foreach (var n in npc.EntityGlobals) {//遍历所有GlobalNPC
                if (n is not DeathTrackingNPC tracker) {//检查是否为DeathTrackingNPC的子类
                    continue;
                }
                tracker.OnKill(npc);//调用OnKill方法进行任务完成检查
            }
        }
    }
}
