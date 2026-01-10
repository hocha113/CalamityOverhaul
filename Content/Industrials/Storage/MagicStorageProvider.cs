using CalamityOverhaul.OtherMods.MagicStorage;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Storage
{
    /// <summary>
    /// Magic Storage模组的存储核心提供者实现
    /// 使用反射调用以避免硬依赖，通过懒加载缓存反射结果优化性能
    /// </summary>
    public class MagicStorageProvider : IStorageProvider
    {
        private readonly object _storageHeart;
        private readonly Point16 _position;

        #region 静态反射缓存(懒加载)
        private static MethodInfo _getStorageUnitsMethod;
        private static PropertyInfo _unitInactiveProperty;
        private static PropertyInfo _unitIsFullProperty;
        private static PropertyInfo _unitHasSpaceInStackForProperty;
        private static bool _reflectionInitialized;
        private static readonly object _reflectionLock = new();

        /// <summary>
        /// 初始化反射缓存，只执行一次
        /// </summary>
        private static void EnsureReflectionInitialized(object storageHeart) {
            if (_reflectionInitialized || storageHeart == null) {
                return;
            }

            lock (_reflectionLock) {
                if (_reflectionInitialized) {
                    return;
                }

                try {
                    var heartType = storageHeart.GetType();
                    _getStorageUnitsMethod = heartType.GetMethod("GetStorageUnits");

                    //获取存储单元类型的属性需要先获取一个实例
                    if (_getStorageUnitsMethod != null) {
                        var units = _getStorageUnitsMethod.Invoke(storageHeart, null) as System.Collections.IEnumerable;
                        if (units != null) {
                            foreach (var unit in units) {
                                var unitType = unit.GetType();
                                _unitInactiveProperty = unitType.GetProperty("Inactive");
                                _unitIsFullProperty = unitType.GetProperty("IsFull");
                                _unitHasSpaceInStackForProperty = unitType.GetMethod("HasSpaceInStackFor") != null
                                    ? null : null; //方法不用PropertyInfo
                                break;
                            }
                        }
                    }
                } catch {
                    //反射失败时保持null，后续检查会返回false
                }

                _reflectionInitialized = true;
            }
        }

        /// <summary>
        /// 重置反射缓存，在模组卸载时调用
        /// </summary>
        public static void ResetReflectionCache() {
            lock (_reflectionLock) {
                _getStorageUnitsMethod = null;
                _unitInactiveProperty = null;
                _unitIsFullProperty = null;
                _unitHasSpaceInStackForProperty = null;
                _reflectionInitialized = false;
            }
        }
        #endregion

        public string Identifier => "MagicStorage.StorageHeart";
        public Point16 Position => _position;
        public Vector2 WorldCenter => Position.ToWorldCoordinates() + new Vector2(24, 24);
        public Rectangle HitBox => new Rectangle(Position.X * 16, Position.Y * 16, 48, 48);

        public bool IsValid {
            get {
                if (_storageHeart == null) {
                    return false;
                }
                if (!TileEntity.ByPosition.TryGetValue(_position, out TileEntity te)) {
                    return false;
                }
                return te == _storageHeart;
            }
        }

        public bool HasSpace {
            get {
                if (!IsValid || !MSRef.Has) {
                    return false;
                }

                EnsureReflectionInitialized(_storageHeart);

                if (_getStorageUnitsMethod == null) {
                    return false;
                }

                try {
                    var units = _getStorageUnitsMethod.Invoke(_storageHeart, null) as System.Collections.IEnumerable;
                    if (units == null) {
                        return false;
                    }

                    foreach (var unit in units) {
                        if (_unitInactiveProperty == null || _unitIsFullProperty == null) {
                            continue;
                        }

                        bool inactive = (bool)_unitInactiveProperty.GetValue(unit);
                        bool isFull = (bool)_unitIsFullProperty.GetValue(unit);
                        if (!inactive && !isFull) {
                            return true;
                        }
                    }
                } catch {
                    return false;
                }
                return false;
            }
        }

        public MagicStorageProvider(object storageHeart, Point16 position) {
            _storageHeart = storageHeart;
            _position = position;
            //构造时初始化反射缓存
            EnsureReflectionInitialized(storageHeart);
        }

        /// <summary>
        /// 从TileEntity创建存储提供者
        /// </summary>
        public static MagicStorageProvider FromTileEntity(TileEntity te) {
            if (te == null || !MSRef.Has) {
                return null;
            }
            var heartType = CWRMod.Instance.magicStorage?.Find<ModTileEntity>("TEStorageHeart")?.Type;
            if (heartType == null || te.type != heartType) {
                return null;
            }
            return new MagicStorageProvider(te, te.Position);
        }

        /// <summary>
        /// 在指定范围内查找Magic Storage存储核心
        /// </summary>
        public static MagicStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            if (!MSRef.Has || !ModLoader.HasMod("MagicStorage")) {
                return null;
            }

            try {
                object heart = MSRef.FindMagicStorage(item, position, range);
                if (heart == null) {
                    return null;
                }
                //获取存储核心的位置
                if (heart is TileEntity te) {
                    return new MagicStorageProvider(heart, te.Position);
                }
            } catch {
                return null;
            }
            return null;
        }

        /// <summary>
        /// 获取指定位置的Magic Storage存储核心
        /// </summary>
        /// <param name="position"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public static MagicStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!MSRef.Has || !ModLoader.HasMod("MagicStorage")) {
                return null;
            }
            try {
                object heart = MSRef.GetMagicStorage(item, position);
                if (heart == null) {
                    return null;
                }
                //获取存储核心的位置
                if (heart is TileEntity te) {
                    return new MagicStorageProvider(heart, te.Position);
                }
            } catch {
                return null;
            }
            return null;
        }

        public bool CanAcceptItem(Item item) {
            if (!IsValid || item == null || item.IsAir) {
                return false;
            }
            return HasSpace;
        }

        public bool DepositItem(Item item) {
            if (!CanAcceptItem(item)) {
                return false;
            }
            try {
                MSRef.DepositItemMethod?.Invoke(_storageHeart, [item]);
                return true;
            } catch {
                return false;
            }
        }

        public Item WithdrawItem(int itemType, int count) {
            if (!IsValid || !MSRef.Has) {
                return new Item();
            }

            try {
                //通过反射调用 TEStorageHeart.Withdraw 方法
                if (_storageHeart is TileEntity te) {
                    //获取 Withdraw 方法
                    var withdrawMethod = _storageHeart.GetType().GetMethod("Withdraw",
                        [typeof(Item), typeof(bool)]);

                    if (withdrawMethod != null) {
                        //创建要取出的物品
                        Item toWithdraw = new Item();
                        toWithdraw.SetDefaults(itemType);
                        toWithdraw.stack = count;

                        //调用 Withdraw(Item item, bool keepOneIfFavorite)
                        Item withdrawn = withdrawMethod.Invoke(_storageHeart,
                            new object[] { toWithdraw, false }) as Item;

                        return withdrawn ?? new Item();
                    }
                }

                //方法2：如果上面不可用，尝试使用 TryWithdraw
                var tryWithdrawMethod = _storageHeart.GetType().GetMethod("TryWithdraw");
                if (tryWithdrawMethod != null) {
                    Item toWithdraw = new Item();
                    toWithdraw.SetDefaults(itemType);
                    toWithdraw.stack = count;

                    //TryWithdraw 的参数可能不同，需要检查签名
                    Item withdrawn = tryWithdrawMethod.Invoke(_storageHeart,
                        [toWithdraw, false, null, false]) as Item;

                    return withdrawn ?? new Item();
                }
            } catch {
                return new Item();
            }

            return new Item();
        }

        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid || !MSRef.Has) {
                yield break;
            }
            var items = MSRef.GetStoredItems();
            foreach (var item in items) {
                yield return item;
            }
        }

        public long GetItemCount(int itemType) {
            if (!IsValid || !MSRef.Has) {
                return 0;
            }
            return MSRef.GetItemCount(itemType);
        }

        public void PlayDepositAnimation() {
            //Magic Storage没有吞噬动画
        }
    }
}
