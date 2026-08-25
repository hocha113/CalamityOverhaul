using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.SlimeVats
{
    /// <summary>史莱姆培养槽存储工厂:无物品输入面(水不是物品),管道只从产出仓抽凝胶</summary>
    internal class SlimeVatStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.SlimeVat";
        public int Priority => 5;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = SlimeVatStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return SlimeVatStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>史莱姆培养槽存储:只出不进</summary>
    internal class SlimeVatStorageProvider : IStorageProvider
    {
        private readonly SlimeVatTP _vatTP;
        private readonly Point16 _position;

        private static int _vatTPID = -1;
        private static int VatTPID {
            get {
                if (_vatTPID < 0) {
                    _vatTPID = TPUtils.GetID<SlimeVatTP>();
                }
                return _vatTPID;
            }
        }

        public string Identifier => "CWR.SlimeVat";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _vatTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _vatTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 32, 32);

        public bool IsValid {
            get {
                if (_vatTP == null) {
                    return false;
                }
                return TileProcessorLoader.AutoPositionGetTP(_position, out SlimeVatTP tp) && tp == _vatTP;
            }
        }

        /// <summary>无物品输入面,恒无空间</summary>
        public bool HasSpace => false;

        public SlimeVatStorageProvider(SlimeVatTP vatTP) {
            _vatTP = vatTP;
            _position = vatTP?.Position ?? Point16.NegativeOne;
        }

        /// <summary>范围内最近的培养槽;带物品的存入询问一律不匹配</summary>
        public static SlimeVatStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            if (item.Alives()) {
                return null;
            }

            float rangeSQ = range * range;
            SlimeVatTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != VatTPID || baseTP is not SlimeVatTP tp) {
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

            return nearestTP != null ? new SlimeVatStorageProvider(nearestTP) : null;
        }

        public static SlimeVatStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out SlimeVatTP tp)) {
                return null;
            }
            if (item.Alives()) {
                return null;
            }
            return new SlimeVatStorageProvider(tp);
        }

        public bool CanAcceptItem(Item item) => false;

        public bool DepositItem(Item item) => false;

        /// <summary>从产出仓抽取凝胶</summary>
        public Item WithdrawItem(int itemType, int count) {
            if (!IsValid || count <= 0) {
                return new Item();
            }

            int remaining = count;
            Item result = new Item(itemType, 0);

            for (int i = SlimeVatTP.ProduceSlotCount - 1; i >= 0 && remaining > 0; i--) {
                Item slotItem = _vatTP.Produce[i];
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
                _vatTP.MarkDirty();
            }

            return result;
        }

        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }
            foreach (var item in _vatTP.Produce) {
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
            foreach (var item in _vatTP.Produce) {
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
