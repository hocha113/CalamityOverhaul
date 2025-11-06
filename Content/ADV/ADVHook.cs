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
            if (VaultLoad.LoadenContent) {
                foreach (var n in npc.EntityGlobals) {//遍历所有GlobalNPC
                    if (n is BaseDamageTracker tracker) {//检查是否为BaseDamageTracker的子类
                        tracker.Check(npc);//调用Check方法进行任务完成检查
                    }
                }
            }
            orig.Invoke(npc);
        }
    }
}
