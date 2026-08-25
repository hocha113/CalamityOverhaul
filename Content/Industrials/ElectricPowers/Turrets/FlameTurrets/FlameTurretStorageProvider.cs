using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets.FlameTurrets
{
    /// <summary>火焰塔存储工厂:物品管道可向燃料槽灌入凝胶(只进不出)</summary>
    internal class FlameTurretStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.FlameTurret";
        public int Priority => 5;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = FlameTurretStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return FlameTurretStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>火焰塔燃料槽存储:凝胶只进不出,抽取一律落空</summary>
    internal class FlameTurretStorageProvider : IStorageProvider
    {
        private readonly FlameTurretTP _turretTP;
        private readonly Point16 _position;

        private static int _turretTPID = -1;
        private static int TurretTPID {
            get {
                if (_turretTPID < 0) {
                    _turretTPID = TPUtils.GetID<FlameTurretTP>();
                }
                return _turretTPID;
            }
        }

        public string Identifier => "CWR.FlameTurret";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _turretTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _turretTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 32, 32);

        public bool IsValid {
            get {
                if (_turretTP == null) {
                    return false;
                }
                return TileProcessorLoader.AutoPositionGetTP(_position, out FlameTurretTP tp) && tp == _turretTP;
            }
        }

        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }
                Item fuel = _turretTP.FuelGel;
                return fuel == null || fuel.IsAir || fuel.stack < fuel.maxStack;
            }
        }

        public FlameTurretStorageProvider(FlameTurretTP turretTP) {
            _turretTP = turretTP;
            _position = turretTP?.Position ?? Point16.NegativeOne;
        }

        /// <summary>范围内最近的火焰塔</summary>
        public static FlameTurretStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            float rangeSQ = range * range;
            FlameTurretTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != TurretTPID || baseTP is not FlameTurretTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                var provider = new FlameTurretStorageProvider(tp);
                if (item.Alives() && !provider.CanAcceptItem(item)) {
                    continue;
                }

                if (distSQ < nearestDistSQ) {
                    nearestDistSQ = distSQ;
                    nearestTP = tp;
                }
            }

            return nearestTP != null ? new FlameTurretStorageProvider(nearestTP) : null;
        }

        public static FlameTurretStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out FlameTurretTP tp)) {
                return null;
            }
            var provider = new FlameTurretStorageProvider(tp);
            if (item.Alives() && !provider.CanAcceptItem(item)) {
                return null;
            }
            return provider;
        }

        public bool CanAcceptItem(Item item) {
            if (!IsValid || item == null || item.IsAir || item.type != ItemID.Gel) {
                return false;
            }
            Item fuel = _turretTP.FuelGel;
            return fuel == null || fuel.IsAir || (fuel.type == ItemID.Gel && fuel.stack < fuel.maxStack);
        }

        public bool DepositItem(Item item) {
            if (!CanAcceptItem(item)) {
                return false;
            }

            Item fuel = _turretTP.FuelGel;
            if (fuel == null || fuel.IsAir) {
                _turretTP.FuelGel = item.Clone();
                item.TurnToAir();
                _turretTP.MarkFuelDirty();
                return true;
            }

            int addAmount = Math.Min(item.stack, fuel.maxStack - fuel.stack);
            if (addAmount <= 0) {
                return false;
            }
            fuel.stack += addAmount;
            item.stack -= addAmount;
            if (item.stack <= 0) {
                item.TurnToAir();
            }
            _turretTP.MarkFuelDirty();
            return true;
        }

        /// <summary>燃料槽只进不出</summary>
        public Item WithdrawItem(int itemType, int count) => new Item();

        public IEnumerable<Item> GetStoredItems() {
            yield break;
        }

        public long GetItemCount(int itemType) => 0;

        public void PlayDepositAnimation() {
            //火焰塔没有特定的存入动画
        }
    }
}
