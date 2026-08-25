using InnoVault.Storages;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.Generator.Biomass
{
    /// <summary>生物质机存储工厂:物流管向料仓灌生物质燃料</summary>
    internal class BiomassStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "CWR.BiomassGenerator";
        public int Priority => 5;
        public bool IsAvailable => true;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = BiomassStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item) {
            return BiomassStorageProvider.GetAtPosition(position, item);
        }
    }

    /// <summary>生物质机存储:单燃料槽,只收 BiomassFuel 表内物品</summary>
    internal class BiomassStorageProvider : IStorageProvider
    {
        private readonly BiomassGeneratorTP _generatorTP;
        private readonly Point16 _position;

        private static int _biomassTPID = -1;
        private static int BiomassTPID {
            get {
                if (_biomassTPID < 0) {
                    _biomassTPID = TPUtils.GetID<BiomassGeneratorTP>();
                }
                return _biomassTPID;
            }
        }

        public string Identifier => "CWR.BiomassGenerator";
        public Point16 Position => _position;
        public Vector2 WorldCenter => _generatorTP?.CenterInWorld ?? _position.ToWorldCoordinates();
        public Rectangle HitBox => _generatorTP?.HitBox ?? new Rectangle(_position.X * 16, _position.Y * 16, 32, 32);

        public bool IsValid {
            get {
                if (_generatorTP == null) {
                    return false;
                }
                return TileProcessorLoader.AutoPositionGetTP(_position, out BiomassGeneratorTP tp) && tp == _generatorTP;
            }
        }

        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }

                var data = _generatorTP.BiomassData;
                if (data == null) {
                    return false;
                }

                if (data.FuelItem == null || data.FuelItem.IsAir) {
                    return true;
                }

                return data.FuelItem.stack < data.FuelItem.maxStack;
            }
        }

        public BiomassStorageProvider(BiomassGeneratorTP generatorTP) {
            _generatorTP = generatorTP;
            _position = generatorTP?.Position ?? Point16.NegativeOne;
        }

        public static BiomassStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            float rangeSQ = range * range;
            BiomassGeneratorTP nearestTP = null;
            float nearestDistSQ = float.MaxValue;

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != BiomassTPID || baseTP is not BiomassGeneratorTP tp) {
                    continue;
                }

                float distSQ = MathF.Pow(position.X - tp.Position.X, 2) + MathF.Pow(position.Y - tp.Position.Y, 2);
                if (distSQ > rangeSQ) {
                    continue;
                }

                var provider = new BiomassStorageProvider(tp);
                if (item.Alives() && !provider.CanAcceptItem(item)) {
                    continue;
                }

                if (distSQ < nearestDistSQ) {
                    nearestDistSQ = distSQ;
                    nearestTP = tp;
                }
            }

            return nearestTP != null ? new BiomassStorageProvider(nearestTP) : null;
        }

        public static BiomassStorageProvider GetAtPosition(Point16 position, Item item) {
            if (!TileProcessorLoader.AutoPositionGetTP(position, out BiomassGeneratorTP tp)) {
                return null;
            }
            var provider = new BiomassStorageProvider(tp);
            if (item.Alives() && !provider.CanAcceptItem(item)) {
                return null;
            }
            return provider;
        }

        public bool CanAcceptItem(Item item) {
            if (!IsValid || item == null || item.IsAir) {
                return false;
            }

            if (!BiomassFuel.IsBiomass(item.type)) {
                return false;
            }

            var data = _generatorTP.BiomassData;
            if (data == null) {
                return false;
            }

            if (data.FuelItem == null || data.FuelItem.IsAir) {
                return true;
            }

            return data.FuelItem.type == item.type && data.FuelItem.stack < data.FuelItem.maxStack;
        }

        public bool DepositItem(Item item) {
            if (!CanAcceptItem(item)) {
                return false;
            }

            var data = _generatorTP.BiomassData;

            if (data.FuelItem == null || data.FuelItem.IsAir) {
                data.FuelItem = item.Clone();
                item.stack = 0;
                _generatorTP.SendData();
                return true;
            }

            if (data.FuelItem.type == item.type) {
                int canAdd = data.FuelItem.maxStack - data.FuelItem.stack;
                int toAdd = Math.Min(canAdd, item.stack);
                data.FuelItem.stack += toAdd;
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

            var data = _generatorTP.BiomassData;
            if (data?.FuelItem == null || data.FuelItem.IsAir) {
                return new Item();
            }

            if (data.FuelItem.type != itemType) {
                return new Item();
            }

            int take = Math.Min(count, data.FuelItem.stack);
            Item result = new Item(itemType, take);

            data.FuelItem.stack -= take;
            if (data.FuelItem.stack <= 0) {
                data.FuelItem.TurnToAir();
            }

            _generatorTP.SendData();
            return result;
        }

        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }

            var data = _generatorTP.BiomassData;
            if (data?.FuelItem != null && !data.FuelItem.IsAir) {
                yield return data.FuelItem;
            }
        }

        public long GetItemCount(int itemType) {
            if (!IsValid) {
                return 0;
            }

            var data = _generatorTP.BiomassData;
            if (data?.FuelItem != null && !data.FuelItem.IsAir && data.FuelItem.type == itemType) {
                return data.FuelItem.stack;
            }

            return 0;
        }

        public void PlayDepositAnimation() {
            if (!IsValid || VaultUtils.isServer) {
                return;
            }

            for (int i = 0; i < 5; i++) {
                Vector2 pos = WorldCenter + Main.rand.NextVector2Circular(16, 16);
                Dust dust = Dust.NewDustDirect(pos, 4, 4, Terraria.ID.DustID.JungleGrass, 0, -2, 100, default, 1.3f);
                dust.noGravity = true;
            }
        }
    }
}
