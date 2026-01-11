using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Throwers
{
    /// <summary>
    /// 投掷者存储提供者工厂
    /// </summary>
    internal class ThrowerStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.Thrower";
        public int Priority => 5;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = ThrowerStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return ThrowerStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>
    /// 投掷者的存储提供者实现
    /// </summary>
    internal class ThrowerStorageProvider : IStorageProvider
    {
        private readonly ThrowerTP _throwerTP;
        private readonly Point16 _position;

        //缓存ThrowerTP的ID
        private static int _throwerTPID = -1;
        private static int ThrowerTPID {
            get {
                if (_throwerTPID < 0) {
                    _throwerTPID = TPUtils.GetID<ThrowerTP>();
                }
                return _throwerTPID;
            }
        }

        public string Identifier => "CWR.Thrower";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _throwerTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _throwerTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 32, 32);

        public bool IsValid {
            get {
                if (_throwerTP == null) {
                    return false;
                }
                return TileProcessorLoader.AutoPositionGetTP(_position, out ThrowerTP tp) && tp == _throwerTP;
            }
        }

        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }
                //检查是否有空槽位
                if (_throwerTP.StoredItems.Count < ThrowerTP.MaxSlots) {
                    return true;
                }
                //检查是否有可堆叠空间
                foreach (var item in _throwerTP.StoredItems) {
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

        public ThrowerStorageProvider(ThrowerTP throwerTP) {
            _throwerTP = throwerTP;
            _position = throwerTP?.Position ?? Point16.NegativeOne;
        }

        /// <summary>
        /// 从位置查找ThrowerTP并创建存储提供者
        /// </summary>
        public static ThrowerStorageProvider FromPosition(Point16 position) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out ThrowerTP tp)) {
                return null;
            }
            return new ThrowerStorageProvider(tp);
        }

        /// <summary>
        /// 在指定范围内查找最近的ThrowerTP
        /// </summary>
        public static ThrowerStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            float rangeSQ = range * range;
            ThrowerTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != ThrowerTPID) {
                    continue;
                }

                if (baseTP is not ThrowerTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                var provider = new ThrowerStorageProvider(tp);
                if (item.Alives() && !provider.CanAcceptItem(item)) {
                    continue;
                }

                if (distSQ < nearestDistSQ) {
                    nearestDistSQ = distSQ;
                    nearestTP = tp;
                }
            }

            return nearestTP != null ? new ThrowerStorageProvider(nearestTP) : null;
        }

        /// <summary>
        /// 获取指定位置的ThrowerTP存储提供者
        /// </summary>
        public static ThrowerStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out ThrowerTP tp)) {
                return null;
            }
            var provider = new ThrowerStorageProvider(tp);
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
            foreach (var stored in _throwerTP.StoredItems) {
                if (stored.type == item.type && stored.stack < stored.maxStack) {
                    int addAmount = Math.Min(item.stack, stored.maxStack - stored.stack);
                    stored.stack += addAmount;
                    item.stack -= addAmount;
                    if (item.stack <= 0) {
                        item.TurnToAir();
                        _throwerTP.SendData();
                        return true;
                    }
                }
            }

            //添加新物品
            if (_throwerTP.StoredItems.Count < ThrowerTP.MaxSlots && item.stack > 0) {
                Item newItem = item.Clone();
                _throwerTP.StoredItems.Add(newItem);
                item.TurnToAir();
                _throwerTP.SendData();
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

            for (int i = _throwerTP.StoredItems.Count - 1; i >= 0 && remaining > 0; i--) {
                Item slotItem = _throwerTP.StoredItems[i];
                if (slotItem == null || slotItem.IsAir || slotItem.type != itemType) {
                    continue;
                }

                int take = Math.Min(remaining, slotItem.stack);
                slotItem.stack -= take;
                result.stack += take;
                remaining -= take;

                if (slotItem.stack <= 0) {
                    _throwerTP.StoredItems.RemoveAt(i);
                }
            }

            if (result.stack > 0) {
                result.type = itemType;
                _throwerTP.SendData();
            }

            return result;
        }

        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }
            foreach (var item in _throwerTP.StoredItems) {
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
            foreach (var item in _throwerTP.StoredItems) {
                if (item != null && !item.IsAir && item.type == itemType) {
                    count += item.stack;
                }
            }
            return count;
        }

        public void PlayDepositAnimation() {
            //投掷者没有特定的存入动画
        }
    }
}
