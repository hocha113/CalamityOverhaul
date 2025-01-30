using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Items.Rogue;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;

namespace CalamityOverhaul.Content.RemakeItems
{
    //这个修改实例被用于补救全部移除的副本物品，防止玩家一更新后看到满背包的卸载物品
    internal class RecoverUnloadedItem : ItemOverride, ICWRLoader
    {
        public override int TargetID => ModContent.ItemType<UnloadedItem>();
        public override bool DrawingInfo => false;
        public override bool CanLoadLocalization => false;
        /// <summary>
        /// 需要恢复的卸载物品，从字符键对应到目标物品的ID
        /// </summary>
        internal static Dictionary<string, int> RecoverUnloadedItemDic { get; private set; } = [];
        void ICWRLoader.SetupData() {
            foreach (var rItem in CWRMod.ItemOverrideInstances) {
                if (rItem.TargetID == ModContent.ItemType<UnloadedItem>()) {
                    continue;//不   要   替   换   自   己
                }
                Item ectypeItem = new Item(rItem.TargetID);
                if (ectypeItem.ModItem != null) {
                    string key = "CalamityOverhaul/" + ectypeItem.ModItem.Name + "EcType";
                    RecoverUnloadedItemDic.Add(key, rItem.TargetID);
                }
            }
            RecoverUnloadedItemDic.Add("CalamityOverhaul/BlackMatterStick", ModContent.ItemType<NeutronStarIngot>());
            RecoverUnloadedItemDic.Add("CalamityOverhaul/Gangarus", ModContent.ItemType<SpearOfLonginus>());
        }
        void ICWRLoader.UnLoadData() => RecoverUnloadedItemDic.Clear();
        public override void UpdateInventory(Item item, Player player) {
            UnloadedItem unloadedItem = item.ModItem as UnloadedItem;
            string key = unloadedItem.ModName + "/" + unloadedItem.ItemName;
            if (RecoverUnloadedItemDic.TryGetValue(key, out int targetItemID)) {
                item.ChangeItemType(targetItemID);
            }
        }
    }
}
