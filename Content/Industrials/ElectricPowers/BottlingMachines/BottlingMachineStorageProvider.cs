using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.BottlingMachines
{
    /// <summary>瓶装机存储工厂,物品管道经此对接</summary>
    internal class BottlingMachineStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.BottlingMachine";
        public int Priority => 6;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = BottlingMachineStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return BottlingMachineStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>瓶装机存储:存入看输入槽(仅可处理容器),取出看成品槽</summary>
    internal class BottlingMachineStorageProvider : IStorageProvider
    {
        private readonly BottlingMachineTP _machineTP;
        private readonly Point16 _position;

        private static int _machineTPID = -1;
        private static int MachineTPID {
            get {
                if (_machineTPID < 0) {
                    _machineTPID = TPUtils.GetID<BottlingMachineTP>();
                }
                return _machineTPID;
            }
        }

        public string Identifier => "CWR.BottlingMachine";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _machineTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _machineTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 32, 32);

        public bool IsValid {
            get {
                if (_machineTP == null) {
                    return false;
                }
                return TileProcessorLoader.AutoPositionGetTP(_position, out BottlingMachineTP tp) && tp == _machineTP;
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

        /// <summary>成品槽有货</summary>
        public bool HasOutput {
            get {
                if (!IsValid) {
                    return false;
                }
                return _machineTP.OutputItem != null && !_machineTP.OutputItem.IsAir;
            }
        }

        public BottlingMachineStorageProvider(BottlingMachineTP machineTP) {
            _machineTP = machineTP;
            _position = machineTP?.Position ?? Point16.NegativeOne;
        }

        /// <summary>范围内找瓶装机;item 有效查可存,空 item 查可取</summary>
        public static BottlingMachineStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            float rangeSQ = range * range;
            BottlingMachineTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            bool isDepositQuery = item.Alives();

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != MachineTPID) {
                    continue;
                }
                if (baseTP is not BottlingMachineTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                var provider = new BottlingMachineStorageProvider(tp);
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

            return nearestTP != null ? new BottlingMachineStorageProvider(nearestTP) : null;
        }

        public static BottlingMachineStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out BottlingMachineTP tp)) {
                return null;
            }
            return new BottlingMachineStorageProvider(tp);
        }

        /// <summary>输入槽只接可处理容器</summary>
        public bool CanAcceptItem(Item item) {
            if (!IsValid || item == null || item.IsAir) {
                return false;
            }
            if (!BottlingRecipes.CanProcess(item)) {
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

            Item output = _machineTP.OutputItem;
            if (output == null || output.IsAir || output.type != itemType) {
                return new Item();
            }

            int take = Math.Min(count, output.stack);
            Item result = new Item(itemType, take);

            output.stack -= take;
            if (output.stack <= 0) {
                output.TurnToAir();
            }

            _machineTP.SendData();
            return result;
        }

        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }
            Item output = _machineTP.OutputItem;
            if (output != null && !output.IsAir) {
                yield return output;
            }
        }

        public long GetItemCount(int itemType) {
            if (!IsValid) {
                return 0;
            }
            Item output = _machineTP.OutputItem;
            if (output != null && !output.IsAir && output.type == itemType) {
                return output.stack;
            }
            return 0;
        }

        public void PlayDepositAnimation() {
            if (!IsValid || VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Vector2 pos = WorldCenter + Main.rand.NextVector2Circular(16, 16);
                Dust dust = Dust.NewDustDirect(pos, 4, 4, Terraria.ID.DustID.Water,
                    Main.rand.NextFloat(-1f, 1f), -1.5f, 100, default, 1.1f);
                dust.noGravity = true;
            }
        }
    }
}
