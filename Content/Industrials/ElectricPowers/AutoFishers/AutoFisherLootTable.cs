using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.AutoFishers
{
    /// <summary>浮标处的水域环境,按物块采样与世界坐标推断,不依赖玩家</summary>
    internal readonly struct FishEnvironment
    {
        public readonly bool Honey;
        public readonly bool Ocean;
        public readonly bool Sky;
        public readonly bool Underground;
        public readonly bool Caverns;
        public readonly bool Snow;
        public readonly bool Jungle;
        public readonly bool Desert;
        public readonly bool Corruption;
        public readonly bool Crimson;
        public readonly bool Hallow;
        public readonly bool Dungeon;

        private FishEnvironment(bool honey, bool ocean, bool sky, bool underground, bool caverns,
            bool snow, bool jungle, bool desert, bool corruption, bool crimson, bool hallow, bool dungeon) {
            Honey = honey;
            Ocean = ocean;
            Sky = sky;
            Underground = underground;
            Caverns = caverns;
            Snow = snow;
            Jungle = jungle;
            Desert = desert;
            Corruption = corruption;
            Crimson = crimson;
            Hallow = hallow;
            Dungeon = dungeon;
        }

        //生物群系判定的采样半径与命中阈值
        private const int SampleRadius = 10;
        private const int BiomeThreshold = 12;

        /// <summary>在浮标位置附近采样物块,推断这片水属于什么环境;只读物块,任意线程安全</summary>
        public static FishEnvironment Capture(Point16 waterPoint, bool honey) {
            int snow = 0, jungle = 0, desert = 0, corruption = 0, crimson = 0, hallow = 0, dungeon = 0;

            for (int x = waterPoint.X - SampleRadius; x <= waterPoint.X + SampleRadius; x++) {
                for (int y = waterPoint.Y - SampleRadius; y <= waterPoint.Y + SampleRadius; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile) {
                        continue;
                    }
                    switch (tile.TileType) {
                        case TileID.SnowBlock:
                        case TileID.IceBlock:
                        case TileID.BreakableIce:
                            snow++;
                            break;
                        case TileID.JungleGrass:
                            jungle++;
                            break;
                        case TileID.Sand:
                        case TileID.Sandstone:
                        case TileID.HardenedSand:
                            desert++;
                            break;
                        case TileID.Ebonstone:
                        case TileID.CorruptGrass:
                        case TileID.Ebonsand:
                            corruption++;
                            break;
                        case TileID.Crimstone:
                        case TileID.CrimsonGrass:
                        case TileID.Crimsand:
                            crimson++;
                            break;
                        case TileID.Pearlstone:
                        case TileID.HallowedGrass:
                        case TileID.Pearlsand:
                            hallow++;
                            break;
                        case TileID.BlueDungeonBrick:
                        case TileID.GreenDungeonBrick:
                        case TileID.PinkDungeonBrick:
                            dungeon++;
                            break;
                    }
                }
            }

            int y0 = waterPoint.Y;
            bool sky = y0 < Main.worldSurface * 0.35;
            bool underground = y0 > Main.worldSurface;
            bool caverns = y0 > Main.rockLayer;
            //海洋:两侧边缘的浅层水面
            bool ocean = (waterPoint.X < 380 || waterPoint.X > Main.maxTilesX - 380) && y0 < Main.worldSurface + 50;
            //沙滩沙会把海边误判成沙漠,海洋优先
            if (ocean) {
                desert = 0;
            }

            return new FishEnvironment(honey, ocean, sky, underground, caverns,
                snow >= BiomeThreshold, jungle >= BiomeThreshold, desert >= BiomeThreshold,
                corruption >= BiomeThreshold, crimson >= BiomeThreshold,
                hallow >= BiomeThreshold, dungeon >= BiomeThreshold);
        }
    }

    /// <summary>
    /// 自动钓鱼机的自研掉落表。原版 <c>FishingCheck_RollItemDrop</c> 强依赖真实玩家
    /// (手持鱼竿/背包鱼饵/运气),无人机器走这份按环境+钓力加权的转写表:
    /// 垃圾/宝匣/常见鱼/生物群系鱼/稀有货,任务鱼一律排除
    /// </summary>
    internal static class AutoFisherLootTable
    {
        private struct LootEntry
        {
            public int ItemType;
            public int Weight;
            /// <summary>低于此钓力不出</summary>
            public int MinPower;
        }

        //复用的候选池,单线程(权威端主线程)使用
        private static readonly List<LootEntry> pool = new(32);

        /// <summary>
        /// 掷一次渔获。<paramref name="power"/> 为机器最终钓力(含饵力与水体系数),
        /// <paramref name="rand"/> 由调用方提供(权威端掷骰)
        /// </summary>
        public static int Roll(int power, in FishEnvironment env, UnifiedRandom rand) {
            //蜂蜜水的产出表独立:蜜鳍鱼为主,偶尔捞出玻璃瓶
            if (env.Honey) {
                if (rand.Next(100) < 8) {
                    return ItemID.Bottle;
                }
                return ItemID.Honeyfin;
            }

            //垃圾:低钓力时占比很高,高钓力压到零头
            int junkChance = power < 40 ? 45 - power : 4;
            if (rand.Next(100) < junkChance) {
                return rand.Next(3) switch {
                    0 => ItemID.OldShoe,
                    1 => ItemID.Seaweed,
                    _ => ItemID.TinCan,
                };
            }

            //宝匣:基础概率随钓力小幅上浮
            if (rand.Next(100) < 8 + power / 12) {
                return RollCrate(env, rand);
            }

            return RollFish(power, env, rand);
        }

        private static int RollCrate(in FishEnvironment env, UnifiedRandom rand) {
            bool hard = Main.hardMode;

            //生物群系宝匣优先,四成机会落到通用三阶宝匣
            if (rand.Next(100) >= 40) {
                if (env.Ocean) {
                    return hard ? ItemID.OceanCrateHard : ItemID.OceanCrate;
                }
                if (env.Dungeon) {
                    return hard ? ItemID.DungeonFishingCrateHard : ItemID.DungeonFishingCrate;
                }
                if (env.Sky) {
                    return hard ? ItemID.FloatingIslandFishingCrateHard : ItemID.FloatingIslandFishingCrate;
                }
                if (env.Corruption) {
                    return hard ? ItemID.CorruptFishingCrateHard : ItemID.CorruptFishingCrate;
                }
                if (env.Crimson) {
                    return hard ? ItemID.CrimsonFishingCrateHard : ItemID.CrimsonFishingCrate;
                }
                if (env.Hallow) {
                    return hard ? ItemID.HallowedFishingCrateHard : ItemID.HallowedFishingCrate;
                }
                if (env.Jungle) {
                    return hard ? ItemID.JungleFishingCrateHard : ItemID.JungleFishingCrate;
                }
                if (env.Snow) {
                    return hard ? ItemID.FrozenCrateHard : ItemID.FrozenCrate;
                }
                if (env.Desert) {
                    return hard ? ItemID.OasisCrateHard : ItemID.OasisCrate;
                }
            }

            //通用宝匣:木匣为主,铁匣次之,金匣压轴
            int roll = rand.Next(100);
            if (roll < 62) {
                return hard ? ItemID.WoodenCrateHard : ItemID.WoodenCrate;
            }
            if (roll < 92) {
                return hard ? ItemID.IronCrateHard : ItemID.IronCrate;
            }
            return hard ? ItemID.GoldenCrateHard : ItemID.GoldenCrate;
        }

        private static int RollFish(int power, in FishEnvironment env, UnifiedRandom rand) {
            pool.Clear();

            void Add(int itemType, int weight, int minPower = 0) {
                if (power < minPower) {
                    return;
                }
                pool.Add(new LootEntry { ItemType = itemType, Weight = weight, MinPower = minPower });
            }

            //任何水域的保底常见鱼
            Add(ItemID.Bass, 60);

            if (env.Ocean) {
                Add(ItemID.Tuna, 40);
                Add(ItemID.RedSnapper, 40);
                Add(ItemID.Trout, 30);
                Add(ItemID.Shrimp, 14, 30);
                Add(ItemID.PinkJellyfish, 8, 40);
                Add(ItemID.SawtoothShark, 3, 65);
            }
            if (env.Jungle) {
                Add(ItemID.NeonTetra, 42);
                Add(ItemID.DoubleCod, 22, 30);
                if (env.Underground) {
                    Add(ItemID.VariegatedLardfish, 18, 35);
                }
            }
            if (env.Snow) {
                Add(ItemID.AtlanticCod, 42);
                Add(ItemID.FrostMinnow, 20, 30);
                if (env.Underground) {
                    Add(ItemID.Stinkfish, 12, 35);
                }
            }
            if (env.Desert) {
                Add(ItemID.Flounder, 40);
                Add(ItemID.RockLobster, 40);
                Add(ItemID.Oyster, 8, 45);
            }
            if (env.Corruption) {
                Add(ItemID.Ebonkoi, 36);
                Add(ItemID.PurpleClubberfish, 8, 45);
            }
            if (env.Crimson) {
                Add(ItemID.CrimsonTigerfish, 36);
                Add(ItemID.Hemopiranha, 16, 35);
            }
            if (env.Hallow) {
                Add(ItemID.PrincessFish, 32);
                Add(ItemID.Prismite, 14, 40);
                if (env.Underground) {
                    Add(ItemID.ChaosFish, 8, 50);
                }
            }
            if (env.Sky) {
                Add(ItemID.Damselfish, 40);
            }

            if (env.Caverns || env.Underground) {
                Add(ItemID.ArmoredCavefish, 26, 25);
                Add(ItemID.SpecularFish, 30);
                Add(ItemID.Rockfish, 12, 35);
                Add(ItemID.GoldenCarp, 2, 60);
            }
            else if (!env.Ocean) {
                //地表淡水
                Add(ItemID.Salmon, 34);
                Add(ItemID.SpecularFish, 22);
            }

            //环境无关的稀有货,钓力门槛高
            Add(ItemID.BombFish, 10, 30);
            Add(ItemID.FrogLeg, 3, 55);
            Add(ItemID.BalloonPufferfish, 3, 55);
            Add(ItemID.ZephyrFish, 1, 70);

            //加权抽取
            int totalWeight = 0;
            foreach (LootEntry entry in pool) {
                totalWeight += entry.Weight;
            }
            if (totalWeight <= 0) {
                return ItemID.Bass;
            }

            int pick = rand.Next(totalWeight);
            foreach (LootEntry entry in pool) {
                pick -= entry.Weight;
                if (pick < 0) {
                    return entry.ItemType;
                }
            }
            return ItemID.Bass;
        }
    }
}
