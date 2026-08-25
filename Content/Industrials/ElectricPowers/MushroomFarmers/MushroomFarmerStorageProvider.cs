using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.MushroomFarmers
{
    /// <summary>蘑菇农场机存储工厂:无输入面,管道只从产出仓抽取</summary>
    internal class MushroomFarmerStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.MushroomFarmer";
        public int Priority => 5;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = MushroomFarmerStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return MushroomFarmerStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>蘑菇农场机存储:只出不进(蘑菇没有种子物品,播种走地表扫描)</summary>
    internal class MushroomFarmerStorageProvider : IStorageProvider
    {
        private readonly MushroomFarmerTP _farmerTP;
        private readonly Point16 _position;

        private static int _farmerTPID = -1;
        private static int FarmerTPID {
            get {
                if (_farmerTPID < 0) {
                    _farmerTPID = TPUtils.GetID<MushroomFarmerTP>();
                }
                return _farmerTPID;
            }
        }

        public string Identifier => "CWR.MushroomFarmer";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _farmerTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _farmerTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 32, 32);

        public bool IsValid {
            get {
                if (_farmerTP == null) {
                    return false;
                }
                return TileProcessorLoader.AutoPositionGetTP(_position, out MushroomFarmerTP tp) && tp == _farmerTP;
            }
        }

        /// <summary>无输入面,恒无空间</summary>
        public bool HasSpace => false;

        public MushroomFarmerStorageProvider(MushroomFarmerTP farmerTP) {
            _farmerTP = farmerTP;
            _position = farmerTP?.Position ?? Point16.NegativeOne;
        }

        /// <summary>范围内最近的蘑菇农场机;带物品的存入询问一律不匹配</summary>
        public static MushroomFarmerStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            if (item.Alives()) {
                return null;
            }

            float rangeSQ = range * range;
            MushroomFarmerTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != FarmerTPID || baseTP is not MushroomFarmerTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                if (distSQ < nearestDistSQ) {
                    nearestDistSQ = distSQ;
                    nearestTP = tp;
                }
            }

            return nearestTP != null ? new MushroomFarmerStorageProvider(nearestTP) : null;
        }

        public static MushroomFarmerStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out MushroomFarmerTP tp)) {
                return null;
            }
            if (item.Alives()) {
                return null;
            }
            return new MushroomFarmerStorageProvider(tp);
        }

        public bool CanAcceptItem(Item item) => false;

        public bool DepositItem(Item item) => false;

        /// <summary>从产出仓抽取</summary>
        public Item WithdrawItem(int itemType, int count) {
            if (!IsValid || count <= 0) {
                return new Item();
            }

            int remaining = count;
            Item result = new Item(itemType, 0);

            for (int i = MushroomFarmerTP.ProduceSlotCount - 1; i >= 0 && remaining > 0; i--) {
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
            //只出不进,无存入动画
        }
    }
}
