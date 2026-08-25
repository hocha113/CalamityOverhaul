using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.AutoCrafters
{
    /// <summary>自动合成台存储工厂</summary>
    internal class AutoCrafterStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.AutoCrafter";
        public int Priority => 6;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = AutoCrafterStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return AutoCrafterStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>
    /// 自动合成台存储:纯输出口。进料由机器自己从存储拉取,
    /// 不接受外部塞入(样品槽是身份登记,不是物流面);成品槽可被管道抽走
    /// </summary>
    internal class AutoCrafterStorageProvider : IStorageProvider
    {
        private readonly AutoCrafterTP _crafterTP;
        private readonly Point16 _position;

        private static int _crafterTPID = -1;
        private static int CrafterTPID {
            get {
                if (_crafterTPID < 0) {
                    _crafterTPID = TPUtils.GetID<AutoCrafterTP>();
                }
                return _crafterTPID;
            }
        }

        public string Identifier => "CWR.AutoCrafter";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _crafterTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _crafterTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 48, 48);

        public bool IsValid {
            get {
                if (_crafterTP == null) {
                    return false;
                }
                return TileProcessorLoader.AutoPositionGetTP(_position, out AutoCrafterTP tp) && tp == _crafterTP;
            }
        }

        /// <summary>纯输出口,不收外部物品</summary>
        public bool HasSpace => false;

        /// <summary>成品槽有货</summary>
        public bool HasOutput {
            get {
                if (!IsValid) {
                    return false;
                }

                var data = _crafterTP.CrafterData;
                return data?.OutputItem != null && !data.OutputItem.IsAir;
            }
        }

        public AutoCrafterStorageProvider(AutoCrafterTP crafterTP) {
            _crafterTP = crafterTP;
            _position = crafterTP?.Position ?? Point16.NegativeOne;
        }

        /// <summary>范围内找合成台;只对取出查询有意义(存入恒拒)</summary>
        public static AutoCrafterStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            //存入查询直接短路:纯输出口
            if (item.Alives()) {
                return null;
            }

            float rangeSQ = range * range;
            AutoCrafterTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != CrafterTPID) {
                    continue;
                }

                if (baseTP is not AutoCrafterTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                var provider = new AutoCrafterStorageProvider(tp);
                if (!provider.HasOutput) {
                    continue;
                }

                if (distSQ < nearestDistSQ) {
                    nearestDistSQ = distSQ;
                    nearestTP = tp;
                }
            }

            return nearestTP != null ? new AutoCrafterStorageProvider(nearestTP) : null;
        }

        public static AutoCrafterStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out AutoCrafterTP tp)) {
                return null;
            }
            return new AutoCrafterStorageProvider(tp);
        }

        /// <summary>纯输出口,不收外部物品</summary>
        public bool CanAcceptItem(Item item) => false;

        /// <summary>纯输出口,存入恒失败</summary>
        public bool DepositItem(Item item) => false;

        /// <summary>从成品槽取出物品</summary>
        public Item WithdrawItem(int itemType, int count) {
            if (!IsValid || count <= 0) {
                return new Item();
            }

            var data = _crafterTP.CrafterData;
            if (data?.OutputItem == null || data.OutputItem.IsAir) {
                return new Item();
            }

            if (data.OutputItem.type != itemType) {
                return new Item();
            }

            int take = Math.Min(count, data.OutputItem.stack);
            Item result = new Item(itemType, take);

            data.OutputItem.stack -= take;
            if (data.OutputItem.stack <= 0) {
                data.OutputItem.TurnToAir();
            }

            _crafterTP.SendData();
            return result;
        }

        /// <summary>返回成品槽物品,抽取判断用</summary>
        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }

            var data = _crafterTP.CrafterData;
            if (data?.OutputItem != null && !data.OutputItem.IsAir) {
                yield return data.OutputItem;
            }
        }

        /// <summary>指定类型物品的数量(从成品槽统计)</summary>
        public long GetItemCount(int itemType) {
            if (!IsValid) {
                return 0;
            }

            var data = _crafterTP.CrafterData;
            if (data?.OutputItem != null && !data.OutputItem.IsAir && data.OutputItem.type == itemType) {
                return data.OutputItem.stack;
            }

            return 0;
        }

        public void PlayDepositAnimation() {
        }
    }
}
