using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Crushers
{
    /// <summary>粉碎机存储工厂</summary>
    internal class CrusherStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.Crusher";
        public int Priority => 6;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = CrusherStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return CrusherStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>粉碎机存储;存入看矿料槽,取出看碎矿槽</summary>
    internal class CrusherStorageProvider : IStorageProvider
    {
        private readonly CrusherTP _crusherTP;
        private readonly Point16 _position;

        private static int _crusherTPID = -1;
        private static int CrusherTPID {
            get {
                if (_crusherTPID < 0) {
                    _crusherTPID = TPUtils.GetID<CrusherTP>();
                }
                return _crusherTPID;
            }
        }

        public string Identifier => "CWR.Crusher";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _crusherTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _crusherTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 48, 48);

        public bool IsValid {
            get {
                if (_crusherTP == null) {
                    return false;
                }
                return TileProcessorLoader.AutoPositionGetTP(_position, out CrusherTP tp) && tp == _crusherTP;
            }
        }

        /// <summary>矿料槽有空位</summary>
        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }

                var cruData = _crusherTP.CruData;
                if (cruData == null) {
                    return false;
                }

                if (cruData.InputItem == null || cruData.InputItem.IsAir) {
                    return true;
                }

                return cruData.InputItem.stack < cruData.InputItem.maxStack;
            }
        }

        /// <summary>碎矿槽有货</summary>
        public bool HasOutput {
            get {
                if (!IsValid) {
                    return false;
                }

                var cruData = _crusherTP.CruData;
                return cruData?.OutputItem != null && !cruData.OutputItem.IsAir;
            }
        }

        public CrusherStorageProvider(CrusherTP crusherTP) {
            _crusherTP = crusherTP;
            _position = crusherTP?.Position ?? Point16.NegativeOne;
        }

        /// <summary>范围内找粉碎机;item有效查可存,空item查可取</summary>
        public static CrusherStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            float rangeSQ = range * range;
            CrusherTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            bool isDepositQuery = item.Alives();

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != CrusherTPID) {
                    continue;
                }

                if (baseTP is not CrusherTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                var provider = new CrusherStorageProvider(tp);

                if (isDepositQuery) {
                    if (!provider.CanAcceptItem(item)) {
                        continue;
                    }
                }
                else {
                    if (!provider.HasOutput) {
                        continue;
                    }
                }

                if (distSQ < nearestDistSQ) {
                    nearestDistSQ = distSQ;
                    nearestTP = tp;
                }
            }

            return nearestTP != null ? new CrusherStorageProvider(nearestTP) : null;
        }

        public static CrusherStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out CrusherTP tp)) {
                return null;
            }
            return new CrusherStorageProvider(tp);
        }

        /// <summary>矿料槽可否接(仅可粉碎矿)</summary>
        public bool CanAcceptItem(Item item) {
            if (!IsValid || item == null || item.IsAir) {
                return false;
            }

            if (!CrusherRecipes.CanCrush(item)) {
                return false;
            }

            var cruData = _crusherTP.CruData;
            if (cruData == null) {
                return false;
            }

            if (cruData.InputItem == null || cruData.InputItem.IsAir) {
                return true;
            }

            if (cruData.InputItem.type == item.type && cruData.InputItem.stack < cruData.InputItem.maxStack) {
                return true;
            }

            return false;
        }

        /// <summary>存入物品到矿料槽</summary>
        public bool DepositItem(Item item) {
            if (!CanAcceptItem(item)) {
                return false;
            }

            var cruData = _crusherTP.CruData;

            if (cruData.InputItem == null || cruData.InputItem.IsAir) {
                cruData.InputItem = item.Clone();
                item.stack = 0;
                _crusherTP.SendData();
                return true;
            }

            if (cruData.InputItem.type == item.type) {
                int canAdd = cruData.InputItem.maxStack - cruData.InputItem.stack;
                int toAdd = Math.Min(canAdd, item.stack);
                cruData.InputItem.stack += toAdd;
                item.stack -= toAdd;
                _crusherTP.SendData();
                return true;
            }

            return false;
        }

        /// <summary>从碎矿槽取出物品</summary>
        public Item WithdrawItem(int itemType, int count) {
            if (!IsValid || count <= 0) {
                return new Item();
            }

            var cruData = _crusherTP.CruData;
            if (cruData?.OutputItem == null || cruData.OutputItem.IsAir) {
                return new Item();
            }

            if (cruData.OutputItem.type != itemType) {
                return new Item();
            }

            int take = Math.Min(count, cruData.OutputItem.stack);
            Item result = new Item(itemType, take);

            cruData.OutputItem.stack -= take;
            if (cruData.OutputItem.stack <= 0) {
                cruData.OutputItem.TurnToAir();
            }

            _crusherTP.SendData();
            return result;
        }

        /// <summary>返回碎矿槽物品,抽取判断用</summary>
        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }

            var cruData = _crusherTP.CruData;
            if (cruData?.OutputItem != null && !cruData.OutputItem.IsAir) {
                yield return cruData.OutputItem;
            }
        }

        /// <summary>指定类型物品的数量(从碎矿槽统计)</summary>
        public long GetItemCount(int itemType) {
            if (!IsValid) {
                return 0;
            }

            var cruData = _crusherTP.CruData;
            if (cruData?.OutputItem != null && !cruData.OutputItem.IsAir && cruData.OutputItem.type == itemType) {
                return cruData.OutputItem.stack;
            }

            return 0;
        }

        public void PlayDepositAnimation() {
            if (!IsValid || VaultUtils.isServer) {
                return;
            }

            //石尘落料效果
            for (int i = 0; i < 6; i++) {
                Vector2 pos = WorldCenter + Main.rand.NextVector2Circular(20, 20);
                Dust dust = Dust.NewDustDirect(pos, 4, 4, Terraria.ID.DustID.Stone,
                    Main.rand.NextFloat(-1f, 1f), -1.5f, 100, default, 1.2f);
                dust.noGravity = true;
            }
        }
    }
}
