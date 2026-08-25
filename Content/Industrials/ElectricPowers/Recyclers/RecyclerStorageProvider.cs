using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Recyclers
{
    /// <summary>回收机存储工厂</summary>
    internal class RecyclerStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.Recycler";
        public int Priority => 6;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = RecyclerStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return RecyclerStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>回收机存储;存入看装备槽(仅可拆装备,一次一件),取出看锭料槽</summary>
    internal class RecyclerStorageProvider : IStorageProvider
    {
        private readonly RecyclerTP _recyclerTP;
        private readonly Point16 _position;

        private static int _recyclerTPID = -1;
        private static int RecyclerTPID {
            get {
                if (_recyclerTPID < 0) {
                    _recyclerTPID = TPUtils.GetID<RecyclerTP>();
                }
                return _recyclerTPID;
            }
        }

        public string Identifier => "CWR.Recycler";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _recyclerTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _recyclerTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 48, 48);

        public bool IsValid {
            get {
                if (_recyclerTP == null) {
                    return false;
                }
                return TileProcessorLoader.AutoPositionGetTP(_position, out RecyclerTP tp) && tp == _recyclerTP;
            }
        }

        /// <summary>装备槽有空位(装备不可堆叠,只看空槽)</summary>
        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }

                var recData = _recyclerTP.RecData;
                if (recData == null) {
                    return false;
                }

                return recData.InputItem == null || recData.InputItem.IsAir;
            }
        }

        /// <summary>锭料槽有货</summary>
        public bool HasOutput {
            get {
                if (!IsValid) {
                    return false;
                }

                var recData = _recyclerTP.RecData;
                return recData?.OutputItem != null && !recData.OutputItem.IsAir;
            }
        }

        public RecyclerStorageProvider(RecyclerTP recyclerTP) {
            _recyclerTP = recyclerTP;
            _position = recyclerTP?.Position ?? Point16.NegativeOne;
        }

        /// <summary>范围内找回收机;item有效查可存,空item查可取</summary>
        public static RecyclerStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            float rangeSQ = range * range;
            RecyclerTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            bool isDepositQuery = item.Alives();

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != RecyclerTPID) {
                    continue;
                }

                if (baseTP is not RecyclerTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                var provider = new RecyclerStorageProvider(tp);

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

            return nearestTP != null ? new RecyclerStorageProvider(nearestTP) : null;
        }

        public static RecyclerStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out RecyclerTP tp)) {
                return null;
            }
            return new RecyclerStorageProvider(tp);
        }

        /// <summary>装备槽可否接(仅可拆装备,槽空才收)</summary>
        public bool CanAcceptItem(Item item) {
            if (!IsValid || item == null || item.IsAir) {
                return false;
            }

            if (!RecyclerTables.CanRecycle(item)) {
                return false;
            }

            var recData = _recyclerTP.RecData;
            if (recData == null) {
                return false;
            }

            return recData.InputItem == null || recData.InputItem.IsAir;
        }

        /// <summary>存入装备到装备槽</summary>
        public bool DepositItem(Item item) {
            if (!CanAcceptItem(item)) {
                return false;
            }

            var recData = _recyclerTP.RecData;
            recData.InputItem = item.Clone();
            recData.InputItem.stack = 1;
            item.stack -= 1;
            if (item.stack <= 0) {
                item.TurnToAir();
            }
            _recyclerTP.SendData();
            return true;
        }

        /// <summary>从锭料槽取出物品</summary>
        public Item WithdrawItem(int itemType, int count) {
            if (!IsValid || count <= 0) {
                return new Item();
            }

            var recData = _recyclerTP.RecData;
            if (recData?.OutputItem == null || recData.OutputItem.IsAir) {
                return new Item();
            }

            if (recData.OutputItem.type != itemType) {
                return new Item();
            }

            int take = Math.Min(count, recData.OutputItem.stack);
            Item result = new Item(itemType, take);

            recData.OutputItem.stack -= take;
            if (recData.OutputItem.stack <= 0) {
                recData.OutputItem.TurnToAir();
            }

            _recyclerTP.SendData();
            return result;
        }

        /// <summary>返回锭料槽物品,抽取判断用</summary>
        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }

            var recData = _recyclerTP.RecData;
            if (recData?.OutputItem != null && !recData.OutputItem.IsAir) {
                yield return recData.OutputItem;
            }
        }

        /// <summary>指定类型物品的数量(从锭料槽统计)</summary>
        public long GetItemCount(int itemType) {
            if (!IsValid) {
                return 0;
            }

            var recData = _recyclerTP.RecData;
            if (recData?.OutputItem != null && !recData.OutputItem.IsAir && recData.OutputItem.type == itemType) {
                return recData.OutputItem.stack;
            }

            return 0;
        }

        public void PlayDepositAnimation() {
            if (!IsValid || VaultUtils.isServer) {
                return;
            }

            //电火花落料效果
            for (int i = 0; i < 6; i++) {
                Vector2 pos = WorldCenter + Main.rand.NextVector2Circular(20, 20);
                Dust dust = Dust.NewDustDirect(pos, 4, 4, Terraria.ID.DustID.Electric,
                    Main.rand.NextFloat(-1f, 1f), -1.5f, 100, default, 0.9f);
                dust.noGravity = true;
            }
        }
    }
}
