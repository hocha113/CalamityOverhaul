using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Storage
{
    /// <summary>
    /// 存储提供者工厂接口
    /// 用于创建特定类型的存储提供者
    /// </summary>
    public interface IStorageProviderFactory
    {
        /// <summary>
        /// 工厂的唯一标识符
        /// </summary>
        string Identifier { get; }

        /// <summary>
        /// 工厂优先级，数值越低优先级越高
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 检查此工厂是否可用(比如检查模组是否加载)
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// 在指定范围内查找存储目标
        /// </summary>
        /// <param name="position">搜索中心位置(物块坐标)</param>
        /// <param name="range">搜索范围(像素)</param>
        /// <param name="item">要存储的物品，用于检查是否可以存入</param>
        /// <returns>找到的存储提供者列表</returns>
        IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item);

        /// <summary>
        /// 获取指定位置的存储提供者
        /// </summary>
        /// <param name="position"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        IStorageProvider GetStorageProviders(Point16 position, Item item);
    }

    /// <summary>
    /// 原版箱子存储提供者工厂
    /// </summary>
    public class ChestStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "Vanilla.Chest";
        public int Priority => 0;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            Chest chest;
            if (item.Alives()) {
                //使用扩展方法查找最近的可用箱子
                chest = position.FindClosestChest(range, true, c => c.CanItemBeAddedToChest(item));
            }
            else {
                chest = position.FindClosestChest(range, true, null);
            }

            if (chest != null) {
                int index = Chest.FindChest(chest.x, chest.y);
                if (index >= 0) {
                    yield return new ChestStorageProvider(chest, index);
                }
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            int index = Chest.FindChest(position.X, position.Y);
            if (index >= 0) {
                return new ChestStorageProvider(Main.chest[index], index);
            }
            return null;
        }
    }

    /// <summary>
    /// Magic Storage存储提供者工厂
    /// </summary>
    public class MagicStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "MagicStorage.StorageHeart";
        public int Priority => 10;
        public bool IsAvailable => ModLoader.HasMod("MagicStorage") && OtherMods.MagicStorage.MSRef.Has;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            if (!IsAvailable) {
                yield break;
            }

            var provider = MagicStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            if (!IsAvailable) {
                return null;
            }
            return MagicStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>
    /// 老公爵营地箱子存储提供者工厂
    /// </summary>
    public class OldDuchestStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.OldDuchest";
        public int Priority => 5;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = OldDuchestStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return OldDuchestStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>
    /// 存储提供者注册表
    /// 管理所有已注册的存储提供者工厂
    /// </summary>
    public static class StorageProviderRegistry
    {
        private static readonly List<IStorageProviderFactory> _factories = [];
        private static bool _initialized = false;

        /// <summary>
        /// 初始化注册表，注册内置的存储提供者工厂
        /// </summary>
        public static void Initialize() {
            if (_initialized) {
                return;
            }
            _factories.Clear();

            //注册内置工厂
            foreach (var storageProviderFactory in VaultUtils.GetDerivedInstances<IStorageProviderFactory>()) {
                Register(storageProviderFactory);
            }

            _initialized = true;
        }

        /// <summary>
        /// 注册一个存储提供者工厂
        /// </summary>
        public static void Register(IStorageProviderFactory factory) {
            if (factory == null) {
                return;
            }

            //检查是否已存在同标识符的工厂
            for (int i = 0; i < _factories.Count; i++) {
                if (_factories[i].Identifier == factory.Identifier) {
                    _factories[i] = factory;
                    SortFactories();
                    return;
                }
            }

            _factories.Add(factory);
            SortFactories();
        }

        /// <summary>
        /// 注销一个存储提供者工厂
        /// </summary>
        public static void Unregister(string identifier) {
            _factories.RemoveAll(f => f.Identifier == identifier);
        }

        /// <summary>
        /// 获取所有可用的存储提供者工厂
        /// </summary>
        public static IEnumerable<IStorageProviderFactory> GetAvailableFactories() {
            foreach (var factory in _factories) {
                if (factory.IsAvailable) {
                    yield return factory;
                }
            }
        }

        /// <summary>
        /// 重置注册表
        /// </summary>
        public static void Reset() {
            _factories.Clear();
            _initialized = false;
        }

        private static void SortFactories() {
            _factories.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }
    }
}
