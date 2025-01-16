using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.OtherMods.ImproveGame;

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

    /// <summary>
    /// 这个“Load”是“装载”哦 <br/>
    /// 根据弹药链装载弹药，返回实际装载的弹药列表 <br/>
    /// <b>注意：在该方法执行完成后，物品栏内的弹药即已被消耗！</b>
    /// </summary>
    /// <returns>弹药列表</returns>
    public static bool LoadFromAmmoChain(this Player player, Item weapon, List<Ammo> ammoChain
        , int assignAmmoType, int capacity, out List<Item> pushedAmmo, out int ammoCount) {
        int initialCapacity = capacity; // 记录一下一开始的，用于判断是否有弹药被取出
        pushedAmmo = [];

        #region Step 1: 预处理

        // 设立物品ID -> 其所有物品实例的映射
        var idToInstances = new Dictionary<int, List<Item>>();
        var bigBagItems = GetBigBagItems(player);
        // 补充：A..B 会获取 A到B-1 的元素. 这里这样写是为了保证取弹药顺序符合原版逻辑（先取弹药栏，再取背包，最后大背包）
        var allItems = player.inventory[54..58].Concat(player.inventory[..54]).Concat(bigBagItems)
            .Where(i => i.ammo == assignAmmoType)
            .ToList();
        foreach (var item in allItems) {
            if (!idToInstances.TryGetValue(item.type, out var value))
                idToInstances.Add(item.type, [item]);
            else
                value.Add(item);
        }

        // 根据原版取弹药逻辑，记录取弹药的先后次序，方便“任意弹药”相关处理
        var ammoQueue = new Queue<Item>(); // 用队列存储，先进先出
        foreach (var item in allItems) {
            ammoQueue.Enqueue(item);
        }
        // 注意到 ammoQueue 推入的和存储在 idToInstances 中的是同一个实例，一方 TurnToAir 在另一方也会生效

        #endregion

        #region Step 2: 创建弹药序列

        while (capacity > 0) {
            // 记录一下当前的容量，用于判断是否有弹药被取出
            int oldCapacity = capacity;

            // 读弹药链，创建序列
            foreach (var (itemData, desiredTimes) in ammoChain) {
                if (capacity <= 0)
                    break;

                int itemType = itemData.Item.type;
                int times = Math.Min(capacity, desiredTimes);

                // “任意弹药”，即根据原版逻辑取弹药，取够为止
                if (itemType == UniversalAmmoId) {
                    while (times > 0 && ammoQueue.Count > 0) {
                        var item = ammoQueue.Peek();
                        int ammoType = item.type;
                        if (VaultUtils.ProjectileToSafeAmmoMap.TryGetValue(item.shoot, out int actualAmmo)) {
                            ammoType = actualAmmo;
                        }

                        // 不消耗的独立处理
                        // 按常理来说，这里不需要 AmmunitionIsunlimited 的判断，CombinedHooks.CanConsumeAmmo 就够了
                        // 但是实际测试下来不加这个判断会出问题，可能和灾厄大修内部的其他机制有关，我不理解，所以就不乱动了
                        if (CWRUtils.IsAmmunitionUnlimited(item)) {
                            var clone = new Item(ammoType, times);
                            clone.CWR().AmmoProjectileReturn = false;
                            // 将其压入弹匣
                            pushedAmmo.Add(clone);
                            capacity -= times;
                            break;
                        }

                        // 这里用 > 而不是 >=，因为如果堆叠量等于需求量，应该执行出队操作，也就是 if 外面的逻辑
                        if (item.stack > times) {
                            // 生成一个新的物品实例，堆叠量为需求量
                            var clone = new Item(ammoType, times);
                            // 原物品减少
                            item.stack -= times;
                            // 将其压入弹匣
                            pushedAmmo.Add(clone);
                            capacity -= times;
                            break;
                        }

                        // 物品实例堆叠量不足，直接压入
                        pushedAmmo.Add(new Item(ammoType, item.stack));
                        // 原物品清空
                        times -= item.stack;
                        capacity -= item.stack;
                        item.TurnToAir();
                        // 该弹药已经取完，弹药实例出队
                        ammoQueue.Dequeue();
                    }

                    continue;
                }

                if (!idToInstances.TryGetValue(itemType, out var instances)) {
                    continue;
                }

                foreach (var item in instances) {
                    int ammoType = item.type;
                    if (VaultUtils.ProjectileToSafeAmmoMap.TryGetValue(item.shoot, out int actualAmmo))
                        ammoType = actualAmmo;

                    // 不消耗的独立处理
                    // 按常理来说，这里不需要 AmmunitionIsunlimited 的判断，CombinedHooks.CanConsumeAmmo 就够了
                    // 但是实际测试下来不加这个判断会出问题，可能和灾厄大修内部的其他机制有关，我不理解，所以就不乱动了
                    if (CWRUtils.IsAmmunitionUnlimited(item)) {
                        var clone = new Item(ammoType, times);
                        clone.CWR().AmmoProjectileReturn = false;
                        // 将其压入弹匣
                        pushedAmmo.Add(clone);
                        capacity -= times;
                        break;
                    }

                    // 该物品堆叠量>=需求量，直接取出相应数量
                    if (item.stack >= times) {
                        // 生成一个新的物品实例，堆叠量为需求量
                        var clone = new Item(ammoType, times);
                        // 原物品减少
                        item.stack -= times;
                        // 将其压入弹匣
                        pushedAmmo.Add(clone);
                        capacity -= times;
                        break;
                    }

                    // 物品实例堆叠量不足，直接压入
                    pushedAmmo.Add(new Item(ammoType, item.stack));
                    // 原物品清空
                    times -= item.stack;
                    capacity -= item.stack;
                    item.TurnToAir();
                }
            }

            // 如果没有弹药被取出，说明背包里没有足够的弹药，直接退出
            if (oldCapacity == capacity) {
                break;
            }
        }

        #endregion

        ammoCount = initialCapacity - capacity;
        // 如果 initialCapacity == capacity，说明没有弹药被取出，那就返回 false，执行灾厄大修原本的操作
        return initialCapacity != capacity;
    }
}