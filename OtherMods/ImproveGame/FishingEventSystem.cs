using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.ImproveGame
{
    internal class FishingEventSystem : ICWRLoader
    {
        //void ICWRLoader.SetupData() {
        //    if (ModLoader.TryGetMod("ImproveGame", out Mod improveGame)) {
        //        //注册钓鱼事件
        //        improveGame.Call("RegisterFishingEvent",
        //            (Action<object>)OnFishing);
        //    }
        //}

        //private void OnFishing(object argsObj) {
        //    //使用反射获取 FishingEventArgs 类型，避免直接引用
        //    var argsType = argsObj.GetType();

        //    // 获取属性
        //    int itemType = (int)argsType.GetProperty("ItemType").GetValue(argsObj);
        //    int itemStack = (int)argsType.GetProperty("ItemStack").GetValue(argsObj);

        //    //示例1: 将所有物品替换为铁箱
        //    argsType.GetProperty("ItemType").SetValue(argsObj, ItemID.IronCrate);

        //    //示例2: 将所有鱼的数量翻倍
        //    if (itemStack > 0) {
        //        argsType.GetProperty("ItemStack").SetValue(argsObj, itemStack * 2);
        //    }

        //    //示例3: 阻止钓到垃圾物品
        //    if (itemType == ItemID.OldShoe || itemType == ItemID.TinCan) {
        //        argsType.GetProperty("Cancel").SetValue(argsObj, true);
        //    }
        //}

        //void ICWRLoader.SetupData() {
        //    if (!ModLoader.TryGetMod("ImproveGame", out Mod improveGame))
        //        return;

        //    // 定义委托签名并转换
        //    improveGame.Call("RegisterFishingEvent", (Delegate)OnFishingCallback);
        //}

        //private void OnFishingCallback(TileEntity fisher, Player player, ref int itemType, ref int itemStack, ref bool cancel) {
        //    // 直接通过 ref 参数修改
        //    itemType = ItemID.IronCrate;

        //    if (itemType == ItemID.OldShoe)
        //        cancel = true;

        //    if (itemStack > 0)
        //        itemStack *= 2;
        //}
    }
}
