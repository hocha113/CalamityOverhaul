using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.OldDuchests
{
    /// <summary>老箱子存储工厂</summary>
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

    /// <summary>老箱子存储</summary>
    public class OldDuchestStorageProvider : IStorageProvider
    {
        private readonly OldDuchestTP _chestTP;
        private readonly Point16 _position;

        private const int MAX_SLOTS = 240;

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
                return TileProcessorLoader.AutoPositionGetTP(_position, out OldDuchestTP tp) && tp == _chestTP;
            }
        }

        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }
                if (_chestTP.storedItems.Count < MAX_SLOTS) {
                    return true;
                }
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

        public static OldDuchestStorageProvider FromPosition(Point16 position) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out OldDuchestTP tp)) {
                return null;
            }
            return new OldDuchestStorageProvider(tp);
        }

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

        /// <param name="position"></param>
        /// <param name="item"></param>
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

            //closeTimer触发短暂开关动画
            _chestTP.TriggerDepositAnimation();
        }
    }
}