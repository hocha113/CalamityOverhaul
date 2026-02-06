using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.OceanRaiderses
{
    /// <summary>
    /// 海洋吞噬者存储提供者工厂
    /// </summary>
    internal class OceanRaidersStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.OceanRaiders";
        public int Priority => 5;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = OceanRaidersStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return OceanRaidersStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>
    /// 海洋吞噬者的存储提供者实现
    /// </summary>
    internal class OceanRaidersStorageProvider : IStorageProvider
    {
        private readonly OceanRaidersTP _machineTP;
        private readonly Point16 _position;

        //缓存OceanRaidersTP的ID
        private static int _oceanRaidersTPID = -1;
        private static int OceanRaidersTPID {
            get {
                if (_oceanRaidersTPID < 0) {
                    _oceanRaidersTPID = TPUtils.GetID<OceanRaidersTP>();
                }
                return _oceanRaidersTPID;
            }
        }

        public string Identifier => "CWR.OceanRaiders";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _machineTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _machineTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 176, 96);

        public bool IsValid {
            get {
                if (_machineTP == null) {
                    return false;
                }
                //通过TileProcessorLoader验证TP是否仍然有效
                return TileProcessorLoader.AutoPositionGetTP(_position, out OceanRaidersTP tp) && tp == _machineTP;
            }
        }

        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }
                //检查是否有空槽位
                if (_machineTP.storedItems.Count < OceanRaidersTP.maxStorageSlots) {
                    return true;
                }
                //检查是否有可堆叠的空间
                foreach (var item in _machineTP.storedItems) {
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

        public OceanRaidersStorageProvider(OceanRaidersTP machineTP) {
            _machineTP = machineTP;
            _position = machineTP?.Position ?? Point16.NegativeOne;
        }

        /// <summary>
        /// 从位置查找OceanRaidersTP并创建存储提供者
        /// </summary>
        public static OceanRaidersStorageProvider FromPosition(Point16 position) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out OceanRaidersTP tp)) {
                return null;
            }
            return new OceanRaidersStorageProvider(tp);
        }

        /// <summary>
        /// 在指定范围内查找最近的OceanRaidersTP
        /// </summary>
        public static OceanRaidersStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            float rangeSQ = range * range;
            OceanRaidersTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != OceanRaidersTPID) {
                    continue;
                }

                if (baseTP is not OceanRaidersTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                //检查是否可以存入物品
                var provider = new OceanRaidersStorageProvider(tp);
                if (item.Alives() && !provider.CanAcceptItem(item)) {
                    continue;
                }

                if (distSQ < nearestDistSQ) {
                    nearestDistSQ = distSQ;
                    nearestTP = tp;
                }
            }

            return nearestTP != null ? new OceanRaidersStorageProvider(nearestTP) : null;
        }

        /// <summary>
        /// 获取指定位置的OceanRaidersTP存储提供者
        /// </summary>
        public static OceanRaidersStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out OceanRaidersTP tp)) {
                return null;
            }
            var provider = new OceanRaidersStorageProvider(tp);
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

            //尝试堆叠到现有物品
            foreach (var stored in _machineTP.storedItems) {
                if (stored.type == item.type && stored.stack < stored.maxStack) {
                    int addAmount = Math.Min(item.stack, stored.maxStack - stored.stack);
                    stored.stack += addAmount;
                    item.stack -= addAmount;
                    if (item.stack <= 0) {
                        item.TurnToAir();
                        _machineTP.SendData();
                        return true;
                    }
                }
            }

            //添加新物品
            if (_machineTP.storedItems.Count < OceanRaidersTP.maxStorageSlots && item.stack > 0) {
                Item newItem = item.Clone();
                _machineTP.storedItems.Add(newItem);
                item.TurnToAir();
                _machineTP.SendData();
                return true;
            }

            return false;
        }

        public Item WithdrawItem(int itemType, int count) {
            if (!IsValid || count <= 0) {
                return new Item();
            }

            int remaining = count;
            Item result = new Item(itemType, 0);

            for (int i = _machineTP.storedItems.Count - 1; i >= 0 && remaining > 0; i--) {
                Item slotItem = _machineTP.storedItems[i];
                if (slotItem == null || slotItem.IsAir || slotItem.type != itemType) {
                    continue;
                }

                int take = Math.Min(remaining, slotItem.stack);
                slotItem.stack -= take;
                result.stack += take;
                remaining -= take;

                if (slotItem.stack <= 0) {
                    _machineTP.storedItems.RemoveAt(i);
                }
            }

            if (result.stack > 0) {
                result.type = itemType;
                _machineTP.SendData();
            }

            return result;
        }

        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }
            foreach (var item in _machineTP.storedItems) {
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
            foreach (var item in _machineTP.storedItems) {
                if (item != null && !item.IsAir && item.type == itemType) {
                    count += item.stack;
                }
            }
            return count;
        }

        public void PlayDepositAnimation() {
            //海洋吞噬者没有特定的存入动画
        }
    }
}
