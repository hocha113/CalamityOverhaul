using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.HerbFarmers
{
    /// <summary>草药农场机存储工厂:管道灌入种子,抽取产出</summary>
    internal class HerbFarmerStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.HerbFarmer";
        public int Priority => 5;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = HerbFarmerStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return HerbFarmerStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>草药农场机存储:种子仓只进,产出仓只出</summary>
    internal class HerbFarmerStorageProvider : IStorageProvider
    {
        private readonly HerbFarmerTP _farmerTP;
        private readonly Point16 _position;

        private static int _farmerTPID = -1;
        private static int FarmerTPID {
            get {
                if (_farmerTPID < 0) {
                    _farmerTPID = TPUtils.GetID<HerbFarmerTP>();
                }
                return _farmerTPID;
            }
        }

        public string Identifier => "CWR.HerbFarmer";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _farmerTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _farmerTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 32, 32);

        public bool IsValid {
            get {
                if (_farmerTP == null) {
                    return false;
                }
                return TileProcessorLoader.AutoPositionGetTP(_position, out HerbFarmerTP tp) && tp == _farmerTP;
            }
        }

        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }
                foreach (var item in _farmerTP.Seeds) {
                    if (item == null || item.IsAir || item.stack < item.maxStack) {
                        return true;
                    }
                }
                return false;
            }
        }

        public HerbFarmerStorageProvider(HerbFarmerTP farmerTP) {
            _farmerTP = farmerTP;
            _position = farmerTP?.Position ?? Point16.NegativeOne;
        }

        /// <summary>范围内最近的农场机</summary>
        public static HerbFarmerStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            float rangeSQ = range * range;
            HerbFarmerTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != FarmerTPID || baseTP is not HerbFarmerTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                var provider = new HerbFarmerStorageProvider(tp);
                if (item.Alives() && !provider.CanAcceptItem(item)) {
                    continue;
                }

                if (distSQ < nearestDistSQ) {
                    nearestDistSQ = distSQ;
                    nearestTP = tp;
                }
            }

            return nearestTP != null ? new HerbFarmerStorageProvider(nearestTP) : null;
        }

        public static HerbFarmerStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out HerbFarmerTP tp)) {
                return null;
            }
            var provider = new HerbFarmerStorageProvider(tp);
            if (item.Alives() && !provider.CanAcceptItem(item)) {
                return null;
            }
            return provider;
        }

        public bool CanAcceptItem(Item item) {
            if (!IsValid || !HerbFarmerTP.IsHerbSeed(item)) {
                return false;
            }

            foreach (var stored in _farmerTP.Seeds) {
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

            //先堆叠同类种子
            foreach (var stored in _farmerTP.Seeds) {
                if (stored == null || stored.IsAir || stored.type != item.type || stored.stack >= stored.maxStack) {
                    continue;
                }
                int addAmount = Math.Min(item.stack, stored.maxStack - stored.stack);
                stored.stack += addAmount;
                item.stack -= addAmount;
                if (item.stack <= 0) {
                    item.TurnToAir();
                    _farmerTP.MarkDirty();
                    return true;
                }
            }

            //空槽收纳
            for (int i = 0; i < HerbFarmerTP.SeedSlotCount; i++) {
                Item stored = _farmerTP.Seeds[i];
                if (stored != null && !stored.IsAir) {
                    continue;
                }
                _farmerTP.Seeds[i] = item.Clone();
                item.TurnToAir();
                _farmerTP.MarkDirty();
                return true;
            }

            return false;
        }

        /// <summary>从产出仓抽取</summary>
        public Item WithdrawItem(int itemType, int count) {
            if (!IsValid || count <= 0) {
                return new Item();
            }

            int remaining = count;
            Item result = new Item(itemType, 0);

            for (int i = HerbFarmerTP.ProduceSlotCount - 1; i >= 0 && remaining > 0; i--) {
                Item slotItem = _farmerTP.Produce[i];
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
                _farmerTP.MarkDirty();
            }

            return result;
        }

        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }
            foreach (var item in _farmerTP.Produce) {
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
            foreach (var item in _farmerTP.Produce) {
                if (item != null && !item.IsAir && item.type == itemType) {
                    count += item.stack;
                }
            }
            return count;
        }

        public void PlayDepositAnimation() {
            //农场机没有特定的存入动画
        }
    }
}
