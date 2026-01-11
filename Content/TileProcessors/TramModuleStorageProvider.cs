using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.TileProcessors
{
    /// <summary>
    /// 转化物质工作台存储提供者工厂
    /// </summary>
    public class TramModuleStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.TramModule";
        public int Priority => 5;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = TramModuleStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return TramModuleStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>
    /// 转化物质工作台的存储提供者实现
    /// </summary>
    public class TramModuleStorageProvider : IStorageProvider
    {
        private readonly TramModuleTP _moduleTP;
        private readonly Point16 _position;

        //最大存储槽位数
        private const int MAX_SLOTS = 81;

        //缓存TramModuleTP的ID
        private static int _tramModuleTPID = -1;
        private static int TramModuleTPID {
            get {
                if (_tramModuleTPID < 0) {
                    _tramModuleTPID = TPUtils.GetID<TramModuleTP>();
                }
                return _tramModuleTPID;
            }
        }

        public string Identifier => "CWR.TramModule";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _moduleTP?.Center ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _moduleTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 64, 48);

        public bool IsValid {
            get {
                if (_moduleTP == null) {
                    return false;
                }
                //通过TileProcessorLoader验证TP是否仍然有效
                return TileProcessorLoader.AutoPositionGetTP(_position, out TramModuleTP tp) && tp == _moduleTP;
            }
        }

        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }
                //检查是否有空槽位或可堆叠空间
                foreach (var item in _moduleTP.items) {
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

        public TramModuleStorageProvider(TramModuleTP moduleTP) {
            _moduleTP = moduleTP;
            _position = moduleTP?.Position ?? Point16.NegativeOne;
        }

        /// <summary>
        /// 从位置查找TramModuleTP并创建存储提供者
        /// </summary>
        public static TramModuleStorageProvider FromPosition(Point16 position) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out TramModuleTP tp)) {
                return null;
            }
            return new TramModuleStorageProvider(tp);
        }

        /// <summary>
        /// 在指定范围内查找最近的TramModuleTP
        /// </summary>
        public static TramModuleStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            float rangeSQ = range * range;
            TramModuleTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != TramModuleTPID) {
                    continue;
                }

                if (baseTP is not TramModuleTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                //检查是否可以存入物品
                var provider = new TramModuleStorageProvider(tp);
                if (item.Alives() && !provider.CanAcceptItem(item)) {
                    continue;
                }

                if (distSQ < nearestDistSQ) {
                    nearestDistSQ = distSQ;
                    nearestTP = tp;
                }
            }

            return nearestTP != null ? new TramModuleStorageProvider(nearestTP) : null;
        }

        /// <summary>
        /// 获取指定位置的TramModuleTP存储提供者
        /// </summary>
        public static TramModuleStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out TramModuleTP tp)) {
                return null;
            }
            var provider = new TramModuleStorageProvider(tp);
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
            for (int i = 0; i < _moduleTP.items.Length; i++) {
                Item stored = _moduleTP.items[i];
                if (stored != null && !stored.IsAir && stored.type == item.type && stored.stack < stored.maxStack) {
                    int addAmount = Math.Min(item.stack, stored.maxStack - stored.stack);
                    stored.stack += addAmount;
                    item.stack -= addAmount;
                    if (item.stack <= 0) {
                        item.TurnToAir();
                        _moduleTP.SendData();
                        return true;
                    }
                }
            }

            //添加到空槽位
            for (int i = 0; i < _moduleTP.items.Length; i++) {
                if (_moduleTP.items[i] == null || _moduleTP.items[i].IsAir) {
                    _moduleTP.items[i] = item.Clone();
                    item.TurnToAir();
                    _moduleTP.SendData();
                    return true;
                }
            }

            return false;
        }

        public Item WithdrawItem(int itemType, int count) {
            if (!IsValid || count <= 0) {
                return new Item();
            }

            int remaining = count;
            Item result = new Item(itemType, 0);

            for (int i = _moduleTP.items.Length - 1; i >= 0 && remaining > 0; i--) {
                Item slotItem = _moduleTP.items[i];
                if (slotItem == null || slotItem.IsAir || slotItem.type != itemType) {
                    continue;
                }

                int take = Math.Min(remaining, slotItem.stack);
                slotItem.stack -= take;
                result.stack += take;
                remaining -= take;

                if (slotItem.stack <= 0) {
                    _moduleTP.items[i] = new Item();
                }
            }

            if (result.stack > 0) {
                result.type = itemType;
                _moduleTP.SendData();
            }

            return result;
        }

        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }
            foreach (var item in _moduleTP.items) {
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
            foreach (var item in _moduleTP.items) {
                if (item != null && !item.IsAir && item.type == itemType) {
                    count += item.stack;
                }
            }
            return count;
        }

        public void PlayDepositAnimation() {
            //转化物质工作台没有特定的存入动画
        }
    }
}
