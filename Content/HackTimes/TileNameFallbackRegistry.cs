using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>物块名称兜底表</summary>
    internal static class TileNameFallbackRegistry
    {
        private const string LocalizationPrefix = "Mods.CalamityOverhaul.TileNameFallback.";

        private static readonly Dictionary<int, string> localizationKeysByType = [];
        private static readonly Dictionary<TileStyleKey, string> localizationKeysByStyle = [];

        static TileNameFallbackRegistry() {
            RegisterInitialData();
        }

        public static void Register(int tileType, string localizationKey) {
            if (tileType < 0 || string.IsNullOrWhiteSpace(localizationKey)) return;
            localizationKeysByType[tileType] = localizationKey.Trim();
        }

        public static void RegisterStyle(int tileType, int style, string localizationKey) {
            if (tileType < 0 || style < 0 || string.IsNullOrWhiteSpace(localizationKey)) return;
            localizationKeysByStyle[new TileStyleKey(tileType, style)] = localizationKey.Trim();
        }

        public static bool TryGetName(Tile tile, int tileType, out string name) {
            int style = GetBestEffortStyle(tile, tileType);
            if (style >= 0 && localizationKeysByStyle.TryGetValue(new TileStyleKey(tileType, style), out string localizationKey)) {
                name = GetLocalizedName(localizationKey);
                return true;
            }

            if (localizationKeysByType.TryGetValue(tileType, out localizationKey)) {
                name = GetLocalizedName(localizationKey);
                return true;
            }

            name = null;
            return false;
        }

        public static bool TryGetName(int tileType, out string name) {
            if (localizationKeysByType.TryGetValue(tileType, out string localizationKey)) {
                name = GetLocalizedName(localizationKey);
                return true;
            }

            name = null;
            return false;
        }

        private static string GetLocalizedName(string localizationKey) {
            return Language.GetTextValue(LocalizationPrefix + localizationKey);
        }

        private static int GetBestEffortStyle(Tile tile, int tileType) {
            TileObjectData data = TileObjectData.GetTileData(tileType, 0);
            if (data == null) return 0;

            int frameWidth = data.CoordinateWidth + data.CoordinatePadding;
            int frameHeight = data.CoordinateHeights.Length > 0
                ? data.CoordinateHeights[0] + data.CoordinatePadding
                : 0;
            if (frameWidth <= 0 || frameHeight <= 0) return 0;

            int styleWidth = Math.Max(frameWidth * data.Width, frameWidth);
            int styleHeight = Math.Max(frameHeight * data.Height, frameHeight);
            return data.StyleHorizontal
                ? tile.TileFrameX / styleWidth
                : tile.TileFrameY / styleHeight;
        }

        private static void RegisterInitialData() {
            //基础数据来自 terraria.wiki.gg/zh/wiki/图格_ID，后续测试员可按异常样本继续追加
            Register(TileID.Dirt, "Dirt");
            Register(TileID.Stone, "Stone");
            Register(TileID.Grass, "Grass");
            Register(TileID.Plants, "Plants");
            Register(TileID.Torches, "Torch");
            Register(TileID.Trees, "Tree");
            Register(TileID.Iron, "Iron");
            Register(TileID.Copper, "Copper");
            Register(TileID.Gold, "Gold");
            Register(TileID.Silver, "Silver");
            Register(TileID.ClosedDoor, "ClosedDoor");
            Register(TileID.OpenDoor, "OpenDoor");
            Register(TileID.Heart, "Heart");
            Register(TileID.Bottles, "Bottle");
            Register(TileID.Tables, "Table");
            Register(TileID.Chairs, "Chair");
            Register(TileID.Anvils, "Anvil");
            Register(TileID.Furnaces, "Furnace");
            Register(TileID.WorkBenches, "WorkBench");
            Register(TileID.Platforms, "Platform");
            Register(TileID.Saplings, "Sapling");
            Register(TileID.Containers, "Container");
            Register(TileID.Demonite, "Demonite");
            Register(TileID.CorruptGrass, "CorruptGrass");
            Register(TileID.CorruptPlants, "CorruptPlants");
            Register(TileID.Ebonstone, "Ebonstone");
            Register(TileID.DemonAltar, "DemonAltar");
            Register(TileID.Sunflower, "Sunflower");
            Register(TileID.Pots, "Pot");
            Register(TileID.PiggyBank, "PiggyBank");
            Register(TileID.WoodBlock, "WoodBlock");
            Register(TileID.ShadowOrbs, "ShadowOrb");
            Register(TileID.CorruptThorns, "CorruptThorns");
            Register(TileID.Candles, "Candle");
            Register(TileID.Chandeliers, "Chandelier");
            Register(TileID.Jackolanterns, "JackOLantern");
            Register(TileID.Presents, "Present");
            Register(TileID.Meteorite, "Meteorite");
            Register(TileID.GrayBrick, "GrayBrick");
            Register(TileID.RedBrick, "RedBrick");
            Register(TileID.ClayBlock, "ClayBlock");
            Register(TileID.BlueDungeonBrick, "BlueDungeonBrick");
            Register(TileID.HangingLanterns, "HangingLantern");
            Register(TileID.GreenDungeonBrick, "GreenDungeonBrick");
            Register(TileID.PinkDungeonBrick, "PinkDungeonBrick");
            Register(TileID.GoldBrick, "GoldBrick");
            Register(TileID.SilverBrick, "SilverBrick");
            Register(TileID.CopperBrick, "CopperBrick");
            Register(TileID.Spikes, "Spikes");
            Register(TileID.WaterCandle, "WaterCandle");
            Register(TileID.Books, "Book");
            Register(TileID.Cobweb, "Cobweb");
            Register(TileID.Vines, "Vine");
            Register(TileID.Sand, "Sand");
            Register(TileID.Glass, "Glass");
            Register(TileID.Signs, "Sign");
            Register(TileID.Obsidian, "Obsidian");
            Register(TileID.Ash, "Ash");
            Register(TileID.Hellstone, "Hellstone");
            Register(TileID.Mud, "Mud");
            Register(TileID.JungleGrass, "JungleGrass");
            Register(TileID.JunglePlants, "JunglePlants");
            Register(TileID.JungleVines, "JungleVine");
            Register(TileID.Sapphire, "Sapphire");
            Register(TileID.Ruby, "Ruby");
            Register(TileID.Emerald, "Emerald");
            Register(TileID.Topaz, "Topaz");
            Register(TileID.Amethyst, "Amethyst");
            Register(TileID.Diamond, "Diamond");
            Register(TileID.JungleThorns, "JungleThorns");
            Register(TileID.MushroomGrass, "MushroomGrass");
            Register(TileID.MushroomPlants, "MushroomPlants");
            Register(TileID.MushroomTrees, "MushroomTree");
            Register(TileID.Plants2, "Plants2");
            Register(TileID.JunglePlants2, "JunglePlants2");
            Register(TileID.ObsidianBrick, "ObsidianBrick");
            Register(TileID.HellstoneBrick, "HellstoneBrick");
            Register(TileID.Hellforge, "Hellforge");
            Register(TileID.ClayPot, "ClayPot");
            Register(TileID.Beds, "Bed");
            Register(TileID.Cactus, "Cactus");
            Register(TileID.Coral, "Coral");
            Register(TileID.ImmatureHerbs, "ImmatureHerb");
            Register(TileID.MatureHerbs, "MatureHerb");
            Register(TileID.BloomingHerbs, "BloomingHerb");
            Register(TileID.Tombstones, "Tombstone");
            Register(TileID.Loom, "Loom");
            Register(TileID.Pianos, "Piano");
            Register(TileID.Dressers, "Dresser");
            Register(TileID.Benches, "Bench");
            Register(TileID.Bathtubs, "Bathtub");

            RegisterStyle(TileID.Torches, 0, "Torch");
            RegisterStyle(TileID.Torches, 1, "BlueTorch");
            RegisterStyle(TileID.Torches, 2, "RedTorch");
            RegisterStyle(TileID.Torches, 3, "GreenTorch");
            RegisterStyle(TileID.Torches, 4, "PurpleTorch");
            RegisterStyle(TileID.Torches, 5, "WhiteTorch");
            RegisterStyle(TileID.Torches, 6, "YellowTorch");
            RegisterStyle(TileID.Torches, 7, "DemonTorch");
            RegisterStyle(TileID.Torches, 8, "CursedTorch");
            RegisterStyle(TileID.Torches, 9, "IceTorch");
            RegisterStyle(TileID.Torches, 10, "OrangeTorch");
            RegisterStyle(TileID.Torches, 11, "IchorTorch");
            RegisterStyle(TileID.Torches, 12, "UltrabrightTorch");
            RegisterStyle(TileID.Torches, 13, "BoneTorch");
            RegisterStyle(TileID.Torches, 14, "RainbowTorch");
            RegisterStyle(TileID.Torches, 15, "PinkTorch");
            RegisterStyle(TileID.Torches, 16, "DesertTorch");
            RegisterStyle(TileID.Torches, 17, "CoralTorch");
            RegisterStyle(TileID.Torches, 18, "CorruptTorch");
            RegisterStyle(TileID.Torches, 19, "CrimsonTorch");
            RegisterStyle(TileID.Torches, 20, "HallowedTorch");
            RegisterStyle(TileID.Torches, 21, "JungleTorch");
            RegisterStyle(TileID.Torches, 22, "MushroomTorch");
            RegisterStyle(TileID.Torches, 23, "AetherTorch");

            RegisterStyle(TileID.Trees, 0, "ForestTree");
            RegisterStyle(TileID.Trees, 1, "EbonwoodTree");
            RegisterStyle(TileID.Trees, 2, "RichMahoganyTree");
            RegisterStyle(TileID.Trees, 3, "PearlwoodTree");
            RegisterStyle(TileID.Trees, 4, "BorealTree");
            RegisterStyle(TileID.Trees, 5, "ShadewoodTree");
            RegisterStyle(TileID.Trees, 7, "MushroomTree");

            RegisterStyle(TileID.Anvils, 0, "IronAnvil");
            RegisterStyle(TileID.Anvils, 1, "LeadAnvil");
            RegisterStyle(TileID.DemonAltar, 0, "DemonAltar");
            RegisterStyle(TileID.DemonAltar, 1, "CrimsonAltar");

            Register(192, "Tile192");//生命树树叶
            Register(187, "Tile187");//杂草
            Register(352, "Tile352");//荆棘
        }

        private readonly record struct TileStyleKey(int TileType, int Style);
    }
}
