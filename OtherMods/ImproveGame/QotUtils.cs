using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.OtherMods.ImproveGame;

public static class QotUtils
{
    public static bool QotLoaded => ModLoader.HasMod("ImproveGame");
    public static Mod QotInstance => ModLoader.GetMod("ImproveGame");

    /// <summary>
    /// “任意弹药”物品的ID，注意使用这个前应该确保加载了更好的体验 (QotLoaded is true)
    /// </summary>
    public static int UniversalAmmoId => (int)QotInstance.Call("GetUniversalAmmoId");

    /// <summary>
    /// 获取该物品上的弹药链
    /// </summary>
    /// <param name="item">物品实例</param>
    /// <returns>有弹药链则返回弹药列表，无弹药链则返回null</returns>
    public static List<Ammo> GetQotAmmoChain(this Item item) {
        if (!QotLoaded) {
            return null;
        }

        var tag = (TagCompound)QotInstance.Call("GetAmmoChainSequence", item);
        if (tag == null) {
            return null;
        }

        return !tag.TryGet("chain", out List<Ammo> chain) || chain.Count is 0 ? null : chain;
    }

    public static List<Item> GetBigBagItems(this Player player) {
        return !QotLoaded ? null : (List<Item>)QotInstance.Call("GetBigBagItems", player);
    }

    //HoCha113: 这个函数是新整的
    /// <summary>
    /// 返回应该装填的弹药物品集合
    /// </summary>
    /// <param name="player"></param>
    /// <param name="ammoChain"></param>
    /// <param name="assignAmmoType"></param>
    /// <param name="loadenAmmoQuantity"></param>
    /// <returns></returns>
    public static Item[] GetQotAmmoItems(this Player player, List<Ammo> ammoChain, int assignAmmoType, int loadenAmmoQuantity) {
        List<Item> extractedAmmo = new();
        int totalExtracted = 0;
        int safetyCheckIterations = 0;
        const int maxExtracted = 1600;

        #region Step 1: 预处理

        var bigBagItems = player.GetBigBagItems();
        var allItems = player.inventory[54..58]
            .Concat(player.inventory[..54])
            .Concat(bigBagItems)
            .Where(i => i.ammo == assignAmmoType)
            .ToList();

        #endregion

        #region Step 2: 逐步提取弹药

        while (totalExtracted < loadenAmmoQuantity) {
            if (++safetyCheckIterations > maxExtracted) {
                break; //避免无限循环
            }

            var ammoQueue = new Queue<Item>(allItems);
            bool extractedThisRound = false;

            foreach (var (ammoData, desiredAmount) in ammoChain) {
                int ammoType = ammoData.Item.type;
                int remainingAmount = Math.Min(desiredAmount, loadenAmmoQuantity - totalExtracted);

                if (ammoType == UniversalAmmoId) {
                    extractedThisRound |= ExtractUniversalAmmo(ammoQueue, extractedAmmo, ref totalExtracted, remainingAmount);
                    continue;
                }

                extractedThisRound |= ExtractSpecificAmmo(allItems, extractedAmmo, ammoType, ref totalExtracted, remainingAmount);
            }

            if (!extractedThisRound) {
                break; //若本轮未提取任何弹药，说明没有足够弹药，终止
            }
        }

        #endregion

        return [.. extractedAmmo];
    }

    /// <summary>
    /// 处理通用弹药类型（Universal Ammo），遍历玩家的弹药库存，并按需提取
    /// </summary>
    private static bool ExtractUniversalAmmo(Queue<Item> ammoQueue, List<Item> extractedAmmo, ref int totalExtracted, int remainingAmount) {
        bool extracted = false;

        while (remainingAmount > 0 && ammoQueue.Count > 0) {
            var item = ammoQueue.Dequeue();
            int ammoType = item.type;

            if (VaultUtils.ProjectileToSafeAmmoMap.TryGetValue(item.shoot, out int actualAmmo)) {
                ammoType = actualAmmo;
            }

            var newAmmo = new Item(ammoType, Math.Min(item.stack, remainingAmount));
            if (newAmmo.type != ItemID.None) {
                //newAmmo.CWR().IntendAmmoProjectileReturn = !RangedLoader.IsAmmunitionUnlimited(newAmmo);
                newAmmo.CWR().FromUnlimitedAmmo = true;
            }

            extractedAmmo.Add(newAmmo);
            totalExtracted += newAmmo.stack;
            remainingAmount -= newAmmo.stack;
            extracted = true;
        }

        return extracted;
    }

    /// <summary>
    /// 处理特定类型的弹药，从所有物品列表中筛选匹配的物品并提取
    /// </summary>
    private static bool ExtractSpecificAmmo(List<Item> allItems, List<Item> extractedAmmo, int ammoType, ref int totalExtracted, int remainingAmount) {
        bool extracted = false;

        var matchingItems = allItems.Where(i => i.type == ammoType).ToList();
        foreach (var item in matchingItems) {
            if (remainingAmount <= 0) break;

            int resolvedAmmoType = item.type;
            if (VaultUtils.ProjectileToSafeAmmoMap.TryGetValue(item.shoot, out int actualAmmo)) {
                resolvedAmmoType = actualAmmo;
            }

            var newAmmo = new Item(resolvedAmmoType, Math.Min(item.stack, remainingAmount));
            if (newAmmo.type != ItemID.None) {
                //newAmmo.CWR().IntendAmmoProjectileReturn = !RangedLoader.IsAmmunitionUnlimited(newAmmo);
                newAmmo.CWR().FromUnlimitedAmmo = true;
            }

            extractedAmmo.Add(newAmmo);
            totalExtracted += newAmmo.stack;
            remainingAmount -= newAmmo.stack;
            extracted = true;
        }

        return extracted;
    }
}