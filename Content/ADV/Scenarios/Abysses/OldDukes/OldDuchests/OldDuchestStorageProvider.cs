using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.OldDuchests
{
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
    /// 老公爵营地箱子的存储提供者实现
    /// </summary>
    public class OldDuchestStorageProvider : IStorageProvider
    {
        private readonly OldDuchestTP _chestTP;
        private readonly Point16 _position;

        //最大存储槽位数
        private const int MAX_SLOTS = 240;

        //缓存OldDuchestTP的ID
        private static int _oldDuchestTPID = -1;
        private static int OldDuchestTPID {
            get {
                if (_oldDuchestTPID < 0) {
                    _oldDuchestTPID = TPUtils.GetID<OldDuchestTP>();
                }
                return _oldDuchestTPID;
            }
        }

        public string Identifier => "CWR.OldDuchest";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _chestTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _chestTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 96, 64);

        public bool IsValid {
            get {
                if (_chestTP == null) {
                    return false;
                }
                //通过TileProcessorLoader验证TP是否仍然有效
                return TileProcessorLoader.AutoPositionGetTP(_position, out OldDuchestTP tp) && tp == _chestTP;
            }
        }

        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }
                //检查是否有空槽位
                if (_chestTP.storedItems.Count < MAX_SLOTS) {
                    return true;
                }
                //检查是否有可堆叠的空间
                foreach (var item in _chestTP.storedItems) {
                    if (item == null || item.IsAir) {
                        return true;
                    }
                    if (item.stack < item.maxStack) {
                        return true;
                    }
                }
                return false;
            }
        }

        public OldDuchestStorageProvider(OldDuchestTP chestTP) {
            _chestTP = chestTP;
            _position = chestTP?.Position ?? Point16.NegativeOne;
        }

        /// <summary>
        /// 从位置查找OldDuchestTP并创建存储提供者
        /// </summary>
        public static OldDuchestStorageProvider FromPosition(Point16 position) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out OldDuchestTP tp)) {
                return null;
            }
            return new OldDuchestStorageProvider(tp);
        }

        /// <summary>
        /// 在指定范围内查找最近的OldDuchestTP
        /// </summary>
        public static OldDuchestStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            float rangeSQ = range * range;
            OldDuchestTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != OldDuchestTPID) {
                    continue;
                }

                if (baseTP is not OldDuchestTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                //检查是否可以存入物品
                var provider = new OldDuchestStorageProvider(tp);
                if (item.Alives() && !provider.CanAcceptItem(item)) {
                    continue;
                }

                if (distSQ < nearestDistSQ) {
                    nearestDistSQ = distSQ;
                    nearestTP = tp;
                }
            }

            return nearestTP != null ? new OldDuchestStorageProvider(nearestTP) : null;
        }

        /// <summary>
        /// 获取指定位置的OldDuchestTP存储提供者
        /// </summary>
        /// <param name="position"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public static OldDuchestStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out OldDuchestTP tp)) {
                return null;
            }
            var provider = new OldDuchestStorageProvider(tp);
            if (item.Alives() && !provider.CanAcceptItem(item)) {
                return null;
            }
            return provider;
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

            //使用OldDuchestTP自带的堆叠添加方法
            bool success = _chestTP.StackAddItem(item);
            if (success) {
                _chestTP.SyncItemsToUI();
                _chestTP.SendData();
            }
            return success;
        }

        public Item WithdrawItem(int itemType, int count) {
            if (!IsValid || count <= 0) {
                return new Item();
            }

            int remaining = count;
            Item result = new Item(itemType, 0);

            for (int i = _chestTP.storedItems.Count - 1; i >= 0 && remaining > 0; i--) {
                Item slotItem = _chestTP.storedItems[i];
                if (slotItem == null || slotItem.IsAir || slotItem.type != itemType) {
                    continue;
                }

                int take = Math.Min(remaining, slotItem.stack);
                slotItem.stack -= take;
                result.stack += take;
                remaining -= take;

                if (slotItem.stack <= 0) {
                    _chestTP.storedItems.RemoveAt(i);
                }
            }

            if (result.stack > 0) {
                result.type = itemType;
                _chestTP.SyncItemsToUI();
                _chestTP.SendData();
            }

            return result;
        }

        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }
            foreach (var item in _chestTP.storedItems) {
                if (item != null && !item.IsAir) {
                    yield return item;
                }
            }
        }

        public long GetItemCount(int itemType) {
            if (!IsValid) {
                return 0;
            }
            long count = 0;
            foreach (var item in _chestTP.storedItems) {
                if (item != null && !item.IsAir && item.type == itemType) {
                    count += item.stack;
                }
            }
            return count;
        }

        public void PlayDepositAnimation() {
            if (!IsValid || _chestTP.isOpen) {
                return;
            }

            //触发短暂的开关动画，利用OldDuchestTP的closeTimer机制
            //通过反射调用私有方法UpdateTileFrame，或者直接设置状态触发动画
            _chestTP.TriggerDepositAnimation();
        }
    }
}