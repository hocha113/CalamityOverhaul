using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ShimmerTransmuters
{
    /// <summary>微光转化槽存储工厂,物品管道经此对接</summary>
    internal class ShimmerTransmuterStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.ShimmerTransmuter";
        public int Priority => 6;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = ShimmerTransmuterStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return ShimmerTransmuterStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>微光转化槽存储:存入看输入槽(仅可转化物),取出看四个输出槽</summary>
    internal class ShimmerTransmuterStorageProvider : IStorageProvider
    {
        private readonly ShimmerTransmuterTP _machineTP;
        private readonly Point16 _position;

        private static int _machineTPID = -1;
        private static int MachineTPID {
            get {
                if (_machineTPID < 0) {
                    _machineTPID = TPUtils.GetID<ShimmerTransmuterTP>();
                }
                return _machineTPID;
            }
        }

        public string Identifier => "CWR.ShimmerTransmuter";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _machineTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _machineTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 48, 48);

        public bool IsValid {
            get {
                if (_machineTP == null) {
                    return false;
                }
                return TileProcessorLoader.AutoPositionGetTP(_position, out ShimmerTransmuterTP tp) && tp == _machineTP;
            }
        }

        /// <summary>输入槽有空位</summary>
        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }
                if (_machineTP.InputItem == null || _machineTP.InputItem.IsAir) {
                    return true;
                }
                return _machineTP.InputItem.stack < _machineTP.InputItem.maxStack;
            }
        }

        /// <summary>任一输出槽有货</summary>
        public bool HasOutput {
            get {
                if (!IsValid) {
                    return false;
                }
                for (int i = 0; i < ShimmerTransmuterTP.OutputSlotCount; i++) {
                    Item output = _machineTP.OutputItems[i];
                    if (output != null && !output.IsAir) {
                        return true;
                    }
                }
                return false;
            }
        }

        public ShimmerTransmuterStorageProvider(ShimmerTransmuterTP machineTP) {
            _machineTP = machineTP;
            _position = machineTP?.Position ?? Point16.NegativeOne;
        }

        /// <summary>范围内找微光转化槽;item 有效查可存,空 item 查可取</summary>
        public static ShimmerTransmuterStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            float rangeSQ = range * range;
            ShimmerTransmuterTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            bool isDepositQuery = item.Alives();

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != MachineTPID) {
                    continue;
                }
                if (baseTP is not ShimmerTransmuterTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                var provider = new ShimmerTransmuterStorageProvider(tp);
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

            return nearestTP != null ? new ShimmerTransmuterStorageProvider(nearestTP) : null;
        }

        public static ShimmerTransmuterStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out ShimmerTransmuterTP tp)) {
                return null;
            }
            return new ShimmerTransmuterStorageProvider(tp);
        }

        /// <summary>输入槽只接可被微光转化的物品</summary>
        public bool CanAcceptItem(Item item) {
            if (!IsValid || item == null || item.IsAir) {
                return false;
            }
            if (!ShimmerTransmuteEngine.CanMachineProcess(item)) {
                return false;
            }

            Item input = _machineTP.InputItem;
            if (input == null || input.IsAir) {
                return true;
            }
            return input.type == item.type && input.stack < input.maxStack;
        }

        public bool DepositItem(Item item) {
            if (!CanAcceptItem(item)) {
                return false;
            }

            Item input = _machineTP.InputItem;
            if (input == null || input.IsAir) {
                _machineTP.InputItem = item.Clone();
                item.stack = 0;
                _machineTP.SendData();
                return true;
            }

            if (input.type == item.type) {
                int canAdd = input.maxStack - input.stack;
                int toAdd = Math.Min(canAdd, item.stack);
                input.stack += toAdd;
                item.stack -= toAdd;
                _machineTP.SendData();
                return true;
            }

            return false;
        }

        public Item WithdrawItem(int itemType, int count) {
            if (!IsValid || count <= 0) {
                return new Item();
            }

            int remain = count;
            int taken = 0;
            for (int i = 0; i < ShimmerTransmuterTP.OutputSlotCount && remain > 0; i++) {
                Item output = _machineTP.OutputItems[i];
                if (output == null || output.IsAir || output.type != itemType) {
                    continue;
                }
                int take = Math.Min(remain, output.stack);
                output.stack -= take;
                if (output.stack <= 0) {
                    output.TurnToAir();
                }
                taken += take;
                remain -= take;
            }

            if (taken <= 0) {
                return new Item();
            }

            _machineTP.SendData();
            return new Item(itemType, taken);
        }

        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }
            for (int i = 0; i < ShimmerTransmuterTP.OutputSlotCount; i++) {
                Item output = _machineTP.OutputItems[i];
                if (output != null && !output.IsAir) {
                    yield return output;
                }
            }
        }

        public long GetItemCount(int itemType) {
            if (!IsValid) {
                return 0;
            }
            long total = 0;
            for (int i = 0; i < ShimmerTransmuterTP.OutputSlotCount; i++) {
                Item output = _machineTP.OutputItems[i];
                if (output != null && !output.IsAir && output.type == itemType) {
                    total += output.stack;
                }
            }
            return total;
        }

        public void PlayDepositAnimation() {
            if (!IsValid || VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Vector2 pos = WorldCenter + Main.rand.NextVector2Circular(16, 16);
                Dust dust = Dust.NewDustDirect(pos, 4, 4, Terraria.ID.DustID.ShimmerSpark,
                    Main.rand.NextFloat(-1f, 1f), -1.5f, 100, default, 1.1f);
                dust.noGravity = true;
            }
        }
    }
}
