using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.AutoFishers
{
    /// <summary>自动钓鱼机存储工厂:管道灌入鱼饵,抽取渔获</summary>
    internal class AutoFisherStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.AutoFisher";
        public int Priority => 5;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = AutoFisherStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return AutoFisherStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>自动钓鱼机存储:鱼饵仓只进,渔获仓只出</summary>
    internal class AutoFisherStorageProvider : IStorageProvider
    {
        private readonly AutoFisherTP _fisherTP;
        private readonly Point16 _position;

        private static int _fisherTPID = -1;
        private static int FisherTPID {
            get {
                if (_fisherTPID < 0) {
                    _fisherTPID = TPUtils.GetID<AutoFisherTP>();
                }
                return _fisherTPID;
            }
        }

        public string Identifier => "CWR.AutoFisher";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _fisherTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _fisherTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 48, 80);

        public bool IsValid {
            get {
                if (_fisherTP == null) {
                    return false;
                }
                return TileProcessorLoader.AutoPositionGetTP(_position, out AutoFisherTP tp) && tp == _fisherTP;
            }
        }

        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }
                foreach (var item in _fisherTP.Baits) {
                    if (item == null || item.IsAir || item.stack < item.maxStack) {
                        return true;
                    }
                }
                return false;
            }
        }

        public AutoFisherStorageProvider(AutoFisherTP fisherTP) {
            _fisherTP = fisherTP;
            _position = fisherTP?.Position ?? Point16.NegativeOne;
        }

        /// <summary>范围内最近的钓鱼机</summary>
        public static AutoFisherStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            float rangeSQ = range * range;
            AutoFisherTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != FisherTPID || baseTP is not AutoFisherTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                var provider = new AutoFisherStorageProvider(tp);
                if (item.Alives() && !provider.CanAcceptItem(item)) {
                    continue;
                }

                if (distSQ < nearestDistSQ) {
                    nearestDistSQ = distSQ;
                    nearestTP = tp;
                }
            }

            return nearestTP != null ? new AutoFisherStorageProvider(nearestTP) : null;
        }

        public static AutoFisherStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out AutoFisherTP tp)) {
                return null;
            }
            var provider = new AutoFisherStorageProvider(tp);
            if (item.Alives() && !provider.CanAcceptItem(item)) {
                return null;
            }
            return provider;
        }

        public bool CanAcceptItem(Item item) {
            if (!IsValid || !AutoFisherTP.IsBait(item)) {
                return false;
            }

            foreach (var stored in _fisherTP.Baits) {
                if (stored == null || stored.IsAir) {
                    return true;
                }
                if (stored.type == item.type && stored.stack < stored.maxStack) {
                    return true;
                }
            }
            return false;
        }

        public bool DepositItem(Item item) {
            if (!CanAcceptItem(item)) {
                return false;
            }

            //先堆叠同类鱼饵
            foreach (var stored in _fisherTP.Baits) {
                if (stored == null || stored.IsAir || stored.type != item.type || stored.stack >= stored.maxStack) {
                    continue;
                }
                int addAmount = Math.Min(item.stack, stored.maxStack - stored.stack);
                stored.stack += addAmount;
                item.stack -= addAmount;
                if (item.stack <= 0) {
                    item.TurnToAir();
                    _fisherTP.MarkDirty();
                    return true;
                }
            }

            //空槽收纳
            for (int i = 0; i < AutoFisherTP.BaitSlotCount; i++) {
                Item stored = _fisherTP.Baits[i];
                if (stored != null && !stored.IsAir) {
                    continue;
                }
                _fisherTP.Baits[i] = item.Clone();
                item.TurnToAir();
                _fisherTP.MarkDirty();
                return true;
            }

            return false;
        }

        /// <summary>从渔获仓抽取</summary>
        public Item WithdrawItem(int itemType, int count) {
            if (!IsValid || count <= 0) {
                return new Item();
            }

            int remaining = count;
            Item result = new Item(itemType, 0);

            for (int i = AutoFisherTP.CatchSlotCount - 1; i >= 0 && remaining > 0; i--) {
                Item slotItem = _fisherTP.Catches[i];
                if (slotItem == null || slotItem.IsAir || slotItem.type != itemType) {
                    continue;
                }

                int take = Math.Min(remaining, slotItem.stack);
                slotItem.stack -= take;
                result.stack += take;
                remaining -= take;

                if (slotItem.stack <= 0) {
                    slotItem.TurnToAir();
                }
            }

            if (result.stack > 0) {
                result.type = itemType;
                _fisherTP.MarkDirty();
            }

            return result;
        }

        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }
            foreach (var item in _fisherTP.Catches) {
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
            foreach (var item in _fisherTP.Catches) {
                if (item != null && !item.IsAir && item.type == itemType) {
                    count += item.stack;
                }
            }
            return count;
        }

        public void PlayDepositAnimation() {
            //钓鱼机没有特定的存入动画
        }
    }
}
