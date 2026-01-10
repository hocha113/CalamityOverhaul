using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.Generator.Thermal
{
    #region MK1 热力发电机
    /// <summary>
    /// 热力发电机存储提供者工厂
    /// </summary>
    internal class ThermalStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.ThermalGenerator";
        public int Priority => 5;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = ThermalStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return ThermalStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>
    /// 热力发电机的存储提供者实现
    /// 用于物流管道向热力发电机输入燃料
    /// </summary>
    internal class ThermalStorageProvider : IStorageProvider
    {
        private readonly ThermalGeneratorTP _generatorTP;
        private readonly Point16 _position;

        private static int _thermalTPID = -1;
        private static int ThermalTPID {
            get {
                if (_thermalTPID < 0) {
                    _thermalTPID = TPUtils.GetID<ThermalGeneratorTP>();
                }
                return _thermalTPID;
            }
        }

        public string Identifier => "CWR.ThermalGenerator";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _generatorTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _generatorTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 32, 32);

        public bool IsValid {
            get {
                if (_generatorTP == null) {
                    return false;
                }
                return TileProcessorLoader.AutoPositionGetTP(_position, out ThermalGeneratorTP tp) && tp == _generatorTP;
            }
        }

        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }

                var thermalData = _generatorTP.ThermalData;
                if (thermalData == null) {
                    return false;
                }

                if (thermalData.FuelItem == null || thermalData.FuelItem.IsAir) {
                    return true;
                }

                return thermalData.FuelItem.stack < thermalData.FuelItem.maxStack;
            }
        }

        public ThermalStorageProvider(ThermalGeneratorTP generatorTP) {
            _generatorTP = generatorTP;
            _position = generatorTP?.Position ?? Point16.NegativeOne;
        }

        public static ThermalStorageProvider FromPosition(Point16 position) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out ThermalGeneratorTP tp)) {
                return null;
            }
            return new ThermalStorageProvider(tp);
        }

        public static ThermalStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            float rangeSQ = range * range;
            ThermalGeneratorTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != ThermalTPID) {
                    continue;
                }

                if (baseTP is not ThermalGeneratorTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                var provider = new ThermalStorageProvider(tp);
                if (item.Alives() && !provider.CanAcceptItem(item)) {
                    continue;
                }

                if (distSQ < nearestDistSQ) {
                    nearestDistSQ = distSQ;
                    nearestTP = tp;
                }
            }

            return nearestTP != null ? new ThermalStorageProvider(nearestTP) : null;
        }

        public static ThermalStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out ThermalGeneratorTP tp)) {
                return null;
            }
            var provider = new ThermalStorageProvider(tp);
            if (item.Alives() && !provider.CanAcceptItem(item)) {
                return null;
            }
            return provider;
        }

        public bool CanAcceptItem(Item item) {
            if (!IsValid || item == null || item.IsAir) {
                return false;
            }

            if (!FuelItems.FuelItemToCombustion.ContainsKey(item.type)) {
                return false;
            }

            var thermalData = _generatorTP.ThermalData;
            if (thermalData == null) {
                return false;
            }

            if (thermalData.FuelItem == null || thermalData.FuelItem.IsAir) {
                return true;
            }

            if (thermalData.FuelItem.type == item.type && thermalData.FuelItem.stack < thermalData.FuelItem.maxStack) {
                return true;
            }

            return false;
        }

        public bool DepositItem(Item item) {
            if (!CanAcceptItem(item)) {
                return false;
            }

            var thermalData = _generatorTP.ThermalData;

            if (thermalData.FuelItem == null || thermalData.FuelItem.IsAir) {
                thermalData.FuelItem = item.Clone();
                item.stack = 0;
                _generatorTP.SendData();
                return true;
            }

            if (thermalData.FuelItem.type == item.type) {
                int canAdd = thermalData.FuelItem.maxStack - thermalData.FuelItem.stack;
                int toAdd = Math.Min(canAdd, item.stack);
                thermalData.FuelItem.stack += toAdd;
                item.stack -= toAdd;
                _generatorTP.SendData();
                return true;
            }

            return false;
        }

        public Item WithdrawItem(int itemType, int count) {
            if (!IsValid || count <= 0) {
                return new Item();
            }

            var thermalData = _generatorTP.ThermalData;
            if (thermalData?.FuelItem == null || thermalData.FuelItem.IsAir) {
                return new Item();
            }

            if (thermalData.FuelItem.type != itemType) {
                return new Item();
            }

            int take = Math.Min(count, thermalData.FuelItem.stack);
            Item result = new Item(itemType, take);

            thermalData.FuelItem.stack -= take;
            if (thermalData.FuelItem.stack <= 0) {
                thermalData.FuelItem.TurnToAir();
            }

            _generatorTP.SendData();
            return result;
        }

        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }

            var thermalData = _generatorTP.ThermalData;
            if (thermalData?.FuelItem != null && !thermalData.FuelItem.IsAir) {
                yield return thermalData.FuelItem;
            }
        }

        public long GetItemCount(int itemType) {
            if (!IsValid) {
                return 0;
            }

            var thermalData = _generatorTP.ThermalData;
            if (thermalData?.FuelItem != null && !thermalData.FuelItem.IsAir && thermalData.FuelItem.type == itemType) {
                return thermalData.FuelItem.stack;
            }

            return 0;
        }

        public void PlayDepositAnimation() {
            if (!IsValid || VaultUtils.isServer) {
                return;
            }

            for (int i = 0; i < 5; i++) {
                Vector2 pos = WorldCenter + Main.rand.NextVector2Circular(16, 16);
                Dust dust = Dust.NewDustDirect(pos, 4, 4, Terraria.ID.DustID.Torch, 0, -2, 100, default, 1.5f);
                dust.noGravity = true;
            }
        }
    }
    #endregion

    #region MK2 热力发电机
    /// <summary>
    /// MK2热力发电机存储提供者工厂
    /// </summary>
    internal class ThermalMK2StorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.ThermalGeneratorMK2";
        public int Priority => 5;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = ThermalMK2StorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return ThermalMK2StorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>
    /// MK2热力发电机的存储提供者实现
    /// 用于物流管道向MK2热力发电机输入燃料
    /// </summary>
    internal class ThermalMK2StorageProvider : IStorageProvider
    {
        private readonly ThermalGeneratorMK2TP _generatorTP;
        private readonly Point16 _position;

        private static int _thermalMK2TPID = -1;
        private static int ThermalMK2TPID {
            get {
                if (_thermalMK2TPID < 0) {
                    _thermalMK2TPID = TPUtils.GetID<ThermalGeneratorMK2TP>();
                }
                return _thermalMK2TPID;
            }
        }

        public string Identifier => "CWR.ThermalGeneratorMK2";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _generatorTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _generatorTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 64, 48);

        public bool IsValid {
            get {
                if (_generatorTP == null) {
                    return false;
                }
                return TileProcessorLoader.AutoPositionGetTP(_position, out ThermalGeneratorMK2TP tp) && tp == _generatorTP;
            }
        }

        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }

                var thermalData = _generatorTP.ThermalData;
                if (thermalData == null) {
                    return false;
                }

                if (thermalData.FuelItem == null || thermalData.FuelItem.IsAir) {
                    return true;
                }

                return thermalData.FuelItem.stack < thermalData.FuelItem.maxStack;
            }
        }

        public ThermalMK2StorageProvider(ThermalGeneratorMK2TP generatorTP) {
            _generatorTP = generatorTP;
            _position = generatorTP?.Position ?? Point16.NegativeOne;
        }

        public static ThermalMK2StorageProvider FromPosition(Point16 position) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out ThermalGeneratorMK2TP tp)) {
                return null;
            }
            return new ThermalMK2StorageProvider(tp);
        }

        public static ThermalMK2StorageProvider FindNearPosition(Point16 position, int range, Item item) {
            float rangeSQ = range * range;
            ThermalGeneratorMK2TP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != ThermalMK2TPID) {
                    continue;
                }

                if (baseTP is not ThermalGeneratorMK2TP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                var provider = new ThermalMK2StorageProvider(tp);
                if (item.Alives() && !provider.CanAcceptItem(item)) {
                    continue;
                }

                if (distSQ < nearestDistSQ) {
                    nearestDistSQ = distSQ;
                    nearestTP = tp;
                }
            }

            return nearestTP != null ? new ThermalMK2StorageProvider(nearestTP) : null;
        }

        public static ThermalMK2StorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out ThermalGeneratorMK2TP tp)) {
                return null;
            }
            var provider = new ThermalMK2StorageProvider(tp);
            if (item.Alives() && !provider.CanAcceptItem(item)) {
                return null;
            }
            return provider;
        }

        public bool CanAcceptItem(Item item) {
            if (!IsValid || item == null || item.IsAir) {
                return false;
            }

            if (!FuelItems.FuelItemToCombustion.ContainsKey(item.type)) {
                return false;
            }

            var thermalData = _generatorTP.ThermalData;
            if (thermalData == null) {
                return false;
            }

            if (thermalData.FuelItem == null || thermalData.FuelItem.IsAir) {
                return true;
            }

            if (thermalData.FuelItem.type == item.type && thermalData.FuelItem.stack < thermalData.FuelItem.maxStack) {
                return true;
            }

            return false;
        }

        public bool DepositItem(Item item) {
            if (!CanAcceptItem(item)) {
                return false;
            }

            var thermalData = _generatorTP.ThermalData;

            if (thermalData.FuelItem == null || thermalData.FuelItem.IsAir) {
                thermalData.FuelItem = item.Clone();
                item.stack = 0;
                _generatorTP.SendData();
                return true;
            }

            if (thermalData.FuelItem.type == item.type) {
                int canAdd = thermalData.FuelItem.maxStack - thermalData.FuelItem.stack;
                int toAdd = Math.Min(canAdd, item.stack);
                thermalData.FuelItem.stack += toAdd;
                item.stack -= toAdd;
                _generatorTP.SendData();
                return true;
            }

            return false;
        }

        public Item WithdrawItem(int itemType, int count) {
            if (!IsValid || count <= 0) {
                return new Item();
            }

            var thermalData = _generatorTP.ThermalData;
            if (thermalData?.FuelItem == null || thermalData.FuelItem.IsAir) {
                return new Item();
            }

            if (thermalData.FuelItem.type != itemType) {
                return new Item();
            }

            int take = Math.Min(count, thermalData.FuelItem.stack);
            Item result = new Item(itemType, take);

            thermalData.FuelItem.stack -= take;
            if (thermalData.FuelItem.stack <= 0) {
                thermalData.FuelItem.TurnToAir();
            }

            _generatorTP.SendData();
            return result;
        }

        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }

            var thermalData = _generatorTP.ThermalData;
            if (thermalData?.FuelItem != null && !thermalData.FuelItem.IsAir) {
                yield return thermalData.FuelItem;
            }
        }

        public long GetItemCount(int itemType) {
            if (!IsValid) {
                return 0;
            }

            var thermalData = _generatorTP.ThermalData;
            if (thermalData?.FuelItem != null && !thermalData.FuelItem.IsAir && thermalData.FuelItem.type == itemType) {
                return thermalData.FuelItem.stack;
            }

            return 0;
        }

        public void PlayDepositAnimation() {
            if (!IsValid || VaultUtils.isServer) {
                return;
            }

            for (int i = 0; i < 8; i++) {
                Vector2 pos = WorldCenter + Main.rand.NextVector2Circular(24, 24);
                Dust dust = Dust.NewDustDirect(pos, 4, 4, Terraria.ID.DustID.Torch, 0, -3, 100, default, 1.8f);
                dust.noGravity = true;
            }
        }
    }
    #endregion
}
