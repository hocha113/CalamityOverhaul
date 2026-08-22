using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.PotionBeacons
{
    /// <summary>药剂信标存储工厂:物流管道可向信标灌入增益药水</summary>
    internal class PotionBeaconStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.PotionBeacon";
        public int Priority => 5;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = PotionBeaconStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return PotionBeaconStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>药剂信标存储:只收增益药水,不支持抽取(药水入舱即入账)</summary>
    internal class PotionBeaconStorageProvider : IStorageProvider
    {
        private readonly PotionBeaconTP _beaconTP;
        private readonly Point16 _position;

        private static int _beaconTPID = -1;
        private static int BeaconTPID {
            get {
                if (_beaconTPID < 0) {
                    _beaconTPID = TPUtils.GetID<PotionBeaconTP>();
                }
                return _beaconTPID;
            }
        }

        public string Identifier => "CWR.PotionBeacon";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _beaconTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _beaconTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 48, 80);

        public bool IsValid {
            get {
                if (_beaconTP == null) {
                    return false;
                }
                return TileProcessorLoader.AutoPositionGetTP(_position, out PotionBeaconTP tp) && tp == _beaconTP;
            }
        }

        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }
                foreach (var item in _beaconTP.Potions) {
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

        public PotionBeaconStorageProvider(PotionBeaconTP beaconTP) {
            _beaconTP = beaconTP;
            _position = beaconTP?.Position ?? Point16.NegativeOne;
        }

        /// <summary>范围内最近的可收纳信标</summary>
        public static PotionBeaconStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            float rangeSQ = range * range;
            PotionBeaconTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != BeaconTPID || baseTP is not PotionBeaconTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                var provider = new PotionBeaconStorageProvider(tp);
                if (item.Alives() && !provider.CanAcceptItem(item)) {
                    continue;
                }

                if (distSQ < nearestDistSQ) {
                    nearestDistSQ = distSQ;
                    nearestTP = tp;
                }
            }

            return nearestTP != null ? new PotionBeaconStorageProvider(nearestTP) : null;
        }

        public static PotionBeaconStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out PotionBeaconTP tp)) {
                return null;
            }
            var provider = new PotionBeaconStorageProvider(tp);
            if (item.Alives() && !provider.CanAcceptItem(item)) {
                return null;
            }
            return provider;
        }

        public bool CanAcceptItem(Item item) {
            if (!IsValid || !PotionBeaconTP.IsValidPotion(item)) {
                return false;
            }

            foreach (var stored in _beaconTP.Potions) {
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

            //先堆叠同类
            foreach (var stored in _beaconTP.Potions) {
                if (stored == null || stored.IsAir || stored.type != item.type || stored.stack >= stored.maxStack) {
                    continue;
                }
                int addAmount = Math.Min(item.stack, stored.maxStack - stored.stack);
                stored.stack += addAmount;
                item.stack -= addAmount;
                if (item.stack <= 0) {
                    item.TurnToAir();
                    _beaconTP.MarkDirty();
                    return true;
                }
            }

            //空槽收纳
            for (int i = 0; i < PotionBeaconTP.SlotCount; i++) {
                Item stored = _beaconTP.Potions[i];
                if (stored != null && !stored.IsAir) {
                    continue;
                }
                _beaconTP.Potions[i] = item.Clone();
                item.TurnToAir();
                _beaconTP.MarkDirty();
                return true;
            }

            return false;
        }

        /// <summary>信标只进不出</summary>
        public Item WithdrawItem(int itemType, int count) => new Item();

        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }
            foreach (var item in _beaconTP.Potions) {
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
            foreach (var item in _beaconTP.Potions) {
                if (item != null && !item.IsAir && item.type == itemType) {
                    count += item.stack;
                }
            }
            return count;
        }

        public void PlayDepositAnimation() {
            //信标没有特定的存入动画
        }
    }
}
