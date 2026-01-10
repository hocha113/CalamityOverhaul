using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.Storage
{
    /// <summary>
    /// 存储系统
    /// 提供统一的存储查找和操作入口
    /// </summary>
    public static class StorageSystem
    {
        /// <summary>
        /// 查找可以存放指定物品的最近存储对象
        /// </summary>
        /// <param name="position">搜索中心位置(物块坐标)</param>
        /// <param name="range">搜索范围(像素)</param>
        /// <param name="item">要存储的物品</param>
        /// <returns>找到的存储提供者，如果没找到返回null</returns>
        public static IStorageProvider FindStorageTarget(Point16 position, int range, Item item) {
            if (!item.Alives()) {
                return null;
            }

            //确保注册表已初始化
            StorageProviderRegistry.Initialize();

            //按优先级遍历所有工厂
            foreach (var factory in StorageProviderRegistry.GetAvailableFactories()) {
                foreach (var provider in factory.FindStorageProviders(position, range, item)) {
                    if (provider != null && provider.IsValid && provider.CanAcceptItem(item)) {
                        return provider;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 查找可以存放指定物品的最近存储对象
        /// </summary>
        /// <param name="position">搜索中心位置(物块坐标)</param>
        /// <param name="range">搜索范围(像素)</param>
        /// <returns>找到的存储提供者，如果没找到返回null</returns>
        public static IStorageProvider FindStorageTarget(Point16 position, int range) {
            //确保注册表已初始化
            StorageProviderRegistry.Initialize();

            //按优先级遍历所有工厂
            foreach (var factory in StorageProviderRegistry.GetAvailableFactories()) {
                foreach (var provider in factory.FindStorageProviders(position, range, new Item())) {
                    if (provider != null && provider.IsValid) {
                        return provider;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 获取指定位置的存储对象
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public static IStorageProvider GetStorageTargetByPoint(Point16 position) {
            //确保注册表已初始化
            StorageProviderRegistry.Initialize();

            //按优先级遍历所有工厂
            foreach (var factory in StorageProviderRegistry.GetAvailableFactories()) {
                var providers = factory.GetStorageProviders(position, new Item());
                if (providers != null && providers.IsValid) {
                    return factory.GetStorageProviders(position, new Item());
                }
            }

            return null;
        }

        /// <summary>
        /// 查找指定范围内所有可用的存储对象
        /// </summary>
        /// <param name="position">搜索中心位置(物块坐标)</param>
        /// <param name="range">搜索范围(像素)</param>
        /// <param name="item">用于检查兼容性的物品，可为null</param>
        /// <returns>找到的所有存储提供者</returns>
        public static IEnumerable<IStorageProvider> FindAllStorageTargets(Point16 position, int range, Item item = null) {
            //确保注册表已初始化
            StorageProviderRegistry.Initialize();

            foreach (var factory in StorageProviderRegistry.GetAvailableFactories()) {
                foreach (var provider in factory.FindStorageProviders(position, range, item ?? new Item())) {
                    if (provider != null && provider.IsValid) {
                        yield return provider;
                    }
                }
            }
        }

        /// <summary>
        /// 尝试将物品存入指定范围内的存储对象
        /// </summary>
        /// <param name="position">搜索中心位置(物块坐标)</param>
        /// <param name="range">搜索范围(像素)</param>
        /// <param name="item">要存储的物品</param>
        /// <returns>存储是否成功</returns>
        public static bool TryDepositItem(Point16 position, int range, Item item) {
            var storage = FindStorageTarget(position, range, item);
            if (storage == null) {
                return false;
            }

            bool success = storage.DepositItem(item);
            if (success) {
                storage.PlayDepositAnimation();
            }
            return success;
        }

        /// <summary>
        /// 在指定范围内的所有存储中搜索指定类型物品的总数量
        /// </summary>
        /// <param name="position">搜索中心位置(物块坐标)</param>
        /// <param name="range">搜索范围(像素)</param>
        /// <param name="itemType">物品类型ID</param>
        /// <returns>物品总数量</returns>
        public static long GetTotalItemCount(Point16 position, int range, int itemType) {
            long total = 0;
            foreach (var provider in FindAllStorageTargets(position, range)) {
                total += provider.GetItemCount(itemType);
            }
            return total;
        }

        /// <summary>
        /// 获取指定范围内所有存储中的物品
        /// </summary>
        /// <param name="position">搜索中心位置(物块坐标)</param>
        /// <param name="range">搜索范围(像素)</param>
        /// <returns>所有物品的枚举</returns>
        public static IEnumerable<Item> GetAllStoredItems(Point16 position, int range) {
            foreach (var provider in FindAllStorageTargets(position, range)) {
                foreach (var item in provider.GetStoredItems()) {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// 初始化存储系统
        /// 应在模组加载时调用
        /// </summary>
        public static void Load() {
            StorageProviderRegistry.Initialize();
        }

        /// <summary>
        /// 卸载存储系统
        /// 应在模组卸载时调用
        /// </summary>
        public static void Unload() {
            StorageProviderRegistry.Reset();
        }
    }
}
