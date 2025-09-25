using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Items.Rogue;
using InnoVault.GameSystem;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;

namespace CalamityOverhaul.Content.RemakeItems
{
    //这个修改实例被用于补救全部移除的副本物品，防止玩家一更新后看到满背包的卸载物品
    internal class RecoverUnloadedItem : ICWRLoader
    {
        public static int TargetID { get; private set; }
        /// <summary>
        /// 需要恢复的卸载物品，从字符键对应到目标物品的ID
        /// </summary>
        internal static Dictionary<string, int> RecoverUnloadedItemDic { get; private set; } = [];
        void ICWRLoader.SetupData() {
            foreach (var rItem in ItemOverride.Instances) {
                if (rItem.Mod != CWRMod.Instance) {
                    continue;
                }
                Item ectypeItem = new Item(rItem.TargetID);
                if (ectypeItem.ModItem != null) {
                    string key = "CalamityOverhaul/" + ectypeItem.ModItem.Name + "EcType";
                    RecoverUnloadedItemDic.TryAdd(key, rItem.TargetID);
                }
            }
            RecoverUnloadedItemDic.Add("CalamityOverhaul/BlackMatterStick", ModContent.ItemType<NeutronStarIngot>());
            RecoverUnloadedItemDic.Add("CalamityOverhaul/Gangarus", ModContent.ItemType<SpearOfLonginus>());
            RecoverUnloadedItemDic.Add("CalamityOverhaul/UEPipelineInput", ModContent.ItemType<UEPipeline>());
            TargetID = ModContent.ItemType<UnloadedItem>();
        }
        void ICWRLoader.UnLoadData() => RecoverUnloadedItemDic?.Clear();
        public static void UpdateInventory(Item item) {
            if (item.type != TargetID) {
                return;
            }
            UnloadedItem unloadedItem = item.ModItem as UnloadedItem;
            string key = unloadedItem.ModName + "/" + unloadedItem.ItemName;
            int origStack = item.stack;
            if (RecoverUnloadedItemDic.TryGetValue(key, out int targetItemID)) {
                item.ChangeItemType(targetItemID);
                item.stack = origStack;
            }
        }
    }
}
