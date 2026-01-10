using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Incinerators
{
    /// <summary>
    /// 电炉存储提供者工厂
    /// </summary>
    internal class IncineratorStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.Incinerator";
        public int Priority => 6;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = IncineratorStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return IncineratorStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>
    /// 电炉的统一存储提供者实现
    /// 存入物品时：检查材料槽（InputItem），只接受可熔炼的物品
    /// 取出物品时：从成品槽（OutputItem）取出
    /// </summary>
    internal class IncineratorStorageProvider : IStorageProvider
    {
        private readonly IncineratorTP _incineratorTP;
        private readonly Point16 _position;

        private static int _incineratorTPID = -1;
        private static int IncineratorTPID {
            get {
                if (_incineratorTPID < 0) {
                    _incineratorTPID = TPUtils.GetID<IncineratorTP>();
                }
                return _incineratorTPID;
            }
        }

        public string Identifier => "CWR.Incinerator";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _incineratorTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _incineratorTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 48, 48);

        public bool IsValid {
            get {
                if (_incineratorTP == null) {
                    return false;
                }
                return TileProcessorLoader.AutoPositionGetTP(_position, out IncineratorTP tp) && tp == _incineratorTP;
            }
        }

        /// <summary>
        /// 检查材料槽是否有空间（用于存入判断）
        /// </summary>
        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }

                var incData = _incineratorTP.IncData;
                if (incData == null) {
                    return false;
                }

                //材料槽为空
                if (incData.InputItem == null || incData.InputItem.IsAir) {
                    return true;
                }

                //检查是否有堆叠空间
                return incData.InputItem.stack < incData.InputItem.maxStack;
            }
        }

        /// <summary>
        /// 检查成品槽是否有物品可取
        /// </summary>
        public bool HasOutput {
            get {
                if (!IsValid) {
                    return false;
                }

                var incData = _incineratorTP.IncData;
                return incData?.OutputItem != null && !incData.OutputItem.IsAir;
            }
        }

        public IncineratorStorageProvider(IncineratorTP incineratorTP) {
            _incineratorTP = incineratorTP;
            _position = incineratorTP?.Position ?? Point16.NegativeOne;
        }

        public static IncineratorStorageProvider FromPosition(Point16 position) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out IncineratorTP tp)) {
                return null;
            }
            return new IncineratorStorageProvider(tp);
        }

        /// <summary>
        /// 在指定范围内查找电炉
        /// 如果item有效，查找可以接受该物品的电炉（材料槽）
        /// 如果item无效/空，查找有成品可取的电炉（成品槽）
        /// </summary>
        public static IncineratorStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            float rangeSQ = range * range;
            IncineratorTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            bool isDepositQuery = item.Alives();

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != IncineratorTPID) {
                    continue;
                }

                if (baseTP is not IncineratorTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                var provider = new IncineratorStorageProvider(tp);

                if (isDepositQuery) {
                    //存入查询：检查材料槽是否可以接受物品
                    if (!provider.CanAcceptItem(item)) {
                        continue;
                    }
                }
                else {
                    //取出查询：检查成品槽是否有物品
                    if (!provider.HasOutput) {
                        continue;
                    }
                }

                if (distSQ < nearestDistSQ) {
                    nearestDistSQ = distSQ;
                    nearestTP = tp;
                }
            }

            return nearestTP != null ? new IncineratorStorageProvider(nearestTP) : null;
        }

        public static IncineratorStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out IncineratorTP tp)) {
                return null;
            }
            return new IncineratorStorageProvider(tp);
        }

        /// <summary>
        /// 检查是否可以接受物品（存入材料槽）
        /// 只接受可熔炼的物品
        /// </summary>
        public bool CanAcceptItem(Item item) {
            if (!IsValid || item == null || item.IsAir) {
                return false;
            }

            //检查是否是可熔炼的物品
            if (!IncineratorRecipes.CanSmelt(item)) {
                return false;
            }

            var incData = _incineratorTP.IncData;
            if (incData == null) {
                return false;
            }

            //材料槽为空
            if (incData.InputItem == null || incData.InputItem.IsAir) {
                return true;
            }

            //相同类型且可堆叠
            if (incData.InputItem.type == item.type && incData.InputItem.stack < incData.InputItem.maxStack) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 存入物品到材料槽
        /// </summary>
        public bool DepositItem(Item item) {
            if (!CanAcceptItem(item)) {
                return false;
            }

            var incData = _incineratorTP.IncData;

            //材料槽为空
            if (incData.InputItem == null || incData.InputItem.IsAir) {
                incData.InputItem = item.Clone();
                item.stack = 0;
                _incineratorTP.SendData();
                return true;
            }

            //相同类型，堆叠
            if (incData.InputItem.type == item.type) {
                int canAdd = incData.InputItem.maxStack - incData.InputItem.stack;
                int toAdd = Math.Min(canAdd, item.stack);
                incData.InputItem.stack += toAdd;
                item.stack -= toAdd;
                _incineratorTP.SendData();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 从成品槽取出物品
        /// </summary>
        public Item WithdrawItem(int itemType, int count) {
            if (!IsValid || count <= 0) {
                return new Item();
            }

            var incData = _incineratorTP.IncData;
            if (incData?.OutputItem == null || incData.OutputItem.IsAir) {
                return new Item();
            }

            //只能取出成品槽中的物品
            if (incData.OutputItem.type != itemType) {
                return new Item();
            }

            int take = Math.Min(count, incData.OutputItem.stack);
            Item result = new Item(itemType, take);

            incData.OutputItem.stack -= take;
            if (incData.OutputItem.stack <= 0) {
                incData.OutputItem.TurnToAir();
            }

            _incineratorTP.SendData();
            return result;
        }

        /// <summary>
        /// 获取存储的物品（返回成品槽的物品，用于抽取判断）
        /// </summary>
        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }

            var incData = _incineratorTP.IncData;
            //返回成品槽的物品，因为这个方法主要用于抽取判断
            if (incData?.OutputItem != null && !incData.OutputItem.IsAir) {
                yield return incData.OutputItem;
            }
        }

        /// <summary>
        /// 获取指定类型物品的数量（从成品槽统计）
        /// </summary>
        public long GetItemCount(int itemType) {
            if (!IsValid) {
                return 0;
            }

            var incData = _incineratorTP.IncData;
            if (incData?.OutputItem != null && !incData.OutputItem.IsAir && incData.OutputItem.type == itemType) {
                return incData.OutputItem.stack;
            }

            return 0;
        }

        public void PlayDepositAnimation() {
            if (!IsValid || VaultUtils.isServer) {
                return;
            }

            //生成火花粒子效果
            for (int i = 0; i < 6; i++) {
                Vector2 pos = WorldCenter + Main.rand.NextVector2Circular(20, 20);
                Dust dust = Dust.NewDustDirect(pos, 4, 4, Terraria.ID.DustID.Torch,
                    Main.rand.NextFloat(-1f, 1f), -2f, 100, default, 1.3f);
                dust.noGravity = true;
            }
        }
    }
}
