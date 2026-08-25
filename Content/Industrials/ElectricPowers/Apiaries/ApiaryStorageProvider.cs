using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Apiaries
{
    /// <summary>养蜂箱存储工厂:管道灌入空瓶,抽取蜂蜜瓶</summary>
    internal class ApiaryStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.Apiary";
        public int Priority => 5;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = ApiaryStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return ApiaryStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>养蜂箱存储:瓶仓只进空瓶,产出仓只出</summary>
    internal class ApiaryStorageProvider : IStorageProvider
    {
        private readonly ApiaryTP _apiaryTP;
        private readonly Point16 _position;

        private static int _apiaryTPID = -1;
        private static int ApiaryTPID {
            get {
                if (_apiaryTPID < 0) {
                    _apiaryTPID = TPUtils.GetID<ApiaryTP>();
                }
                return _apiaryTPID;
            }
        }

        public string Identifier => "CWR.Apiary";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _apiaryTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _apiaryTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 32, 32);

        public bool IsValid {
            get {
                if (_apiaryTP == null) {
                    return false;
                }
                return TileProcessorLoader.AutoPositionGetTP(_position, out ApiaryTP tp) && tp == _apiaryTP;
            }
        }

        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }
                foreach (var item in _apiaryTP.Bottles) {
                    if (item == null || item.IsAir || item.stack < item.maxStack) {
                        return true;
                    }
                }
                return false;
            }
        }

        public ApiaryStorageProvider(ApiaryTP apiaryTP) {
            _apiaryTP = apiaryTP;
            _position = apiaryTP?.Position ?? Point16.NegativeOne;
        }

        /// <summary>范围内最近的养蜂箱</summary>
        public static ApiaryStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            float rangeSQ = range * range;
            ApiaryTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != ApiaryTPID || baseTP is not ApiaryTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                var provider = new ApiaryStorageProvider(tp);
                if (item.Alives() && !provider.CanAcceptItem(item)) {
                    continue;
                }

                if (distSQ < nearestDistSQ) {
                    nearestDistSQ = distSQ;
                    nearestTP = tp;
                }
            }

            return nearestTP != null ? new ApiaryStorageProvider(nearestTP) : null;
        }

        public static ApiaryStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out ApiaryTP tp)) {
                return null;
            }
            var provider = new ApiaryStorageProvider(tp);
            if (item.Alives() && !provider.CanAcceptItem(item)) {
                return null;
            }
            return provider;
        }

        public bool CanAcceptItem(Item item) {
            if (!IsValid || !ApiaryTP.IsEmptyBottle(item)) {
                return false;
            }

            foreach (var stored in _apiaryTP.Bottles) {
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

            //先堆叠同类空瓶
            foreach (var stored in _apiaryTP.Bottles) {
                if (stored == null || stored.IsAir || stored.type != item.type || stored.stack >= stored.maxStack) {
                    continue;
                }
                int addAmount = Math.Min(item.stack, stored.maxStack - stored.stack);
                stored.stack += addAmount;
                item.stack -= addAmount;
                if (item.stack <= 0) {
                    item.TurnToAir();
                    _apiaryTP.MarkDirty();
                    return true;
                }
            }

            //空槽收纳
            for (int i = 0; i < ApiaryTP.BottleSlotCount; i++) {
                Item stored = _apiaryTP.Bottles[i];
                if (stored != null && !stored.IsAir) {
                    continue;
                }
                _apiaryTP.Bottles[i] = item.Clone();
                item.TurnToAir();
                _apiaryTP.MarkDirty();
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

            for (int i = ApiaryTP.ProduceSlotCount - 1; i >= 0 && remaining > 0; i--) {
                Item slotItem = _apiaryTP.Produce[i];
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
                _apiaryTP.MarkDirty();
            }

            return result;
        }

        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }
            foreach (var item in _apiaryTP.Produce) {
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
            foreach (var item in _apiaryTP.Produce) {
                if (item != null && !item.IsAir && item.type == itemType) {
                    count += item.stack;
                }
            }
            return count;
        }

        public void PlayDepositAnimation() {
            if (!IsValid || VaultUtils.isServer) {
                return;
            }

            for (int i = 0; i < 4; i++) {
                Vector2 pos = WorldCenter + Main.rand.NextVector2Circular(14, 14);
                Dust dust = Dust.NewDustDirect(pos, 4, 4, DustID.Honey, 0, -1, 100, default, 1.1f);
                dust.noGravity = true;
            }
        }
    }
}
