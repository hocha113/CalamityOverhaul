using CalamityOverhaul.Content.Industrials.Generator.WindGriven;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using CalamityOverhaul.Content.UIs.OverhaulSettings;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Structures
{
    /// <summary>
    /// 风蚀发电机组的无灾厄回退：程序化生成工业空岛并放置荒野风机<br/>
    /// 灾厄在场时走 <see cref="IndustrializationGen.SpawnWindGrivenGenerator"/> 的空岛实验室锚点方案，不进此类
    /// </summary>
    internal static class IndustrialSkyIslandGen
    {
        //一座已建成空岛的甲板信息
        private readonly struct IslandDeck(int centerX, int deckY, int plateauHalf, int bodyHeight)
        {
            public readonly int CenterX = centerX;
            public readonly int DeckY = deckY;//甲板面所在行，设施站在其上方
            public readonly int PlateauHalf = plateauHalf;//平台半宽
            public readonly int BodyHeight = bodyHeight;
        }

        public static void Generate() {
            float multiplier = WorldGenDensitySave.GetMultiplier("WindGrivenGenerator");
            if (multiplier <= 0f) {
                return;
            }

            int islandCount = Math.Clamp((int)Math.Round((2 + WorldGen.GetWorldSize()) * multiplier), 1, 20);

            //高度带与原版浮岛同层：地表线之上的天空带
            int minY = 90 + WorldGen.GetWorldSize() * 20;
            int maxY = (int)(Main.worldSurface * 0.6);
            if (maxY < minY + 40) {
                maxY = minY + 40;
            }

            float distanceFactor = WorldGenDensitySave.GetDistanceFactor("WindGrivenGenerator");
            int minSpacing = (int)(140 * distanceFactor);

            const int margin = 200;
            int usable = Main.maxTilesX - margin * 2;
            int slotWidth = Math.Max(usable / islandCount, 60);

            List<IslandDeck> decks = [];
            List<int> placedXs = [];

            for (int i = 0; i < islandCount; i++) {
                int width = WorldGen.genRand.Next(28, 45);
                int height = WorldGen.genRand.Next(10, 17);

                for (int attempt = 0; attempt < 120; attempt++) {
                    int left = margin + slotWidth * i + WorldGen.genRand.Next(Math.Max(1, slotWidth - width));
                    left = Math.Clamp(left, margin, Main.maxTilesX - margin - width);
                    int deckY = WorldGen.genRand.Next(minY, maxY - height);
                    int centerX = left + width / 2;

                    if (TooCloseToOthers(placedXs, centerX, minSpacing)) {
                        continue;
                    }
                    //候选箱：横向各留 8 格，上方留 24 格净空（MK2 高 18），下方留垂挂空间
                    if (!AreaIsOpenSky(left - 8, deckY - 24, width + 16, height + 34)) {
                        continue;
                    }

                    decks.Add(BuildIslandBody(left, deckY, width, height));
                    placedXs.Add(centerX);
                    break;
                }
            }

            if (decks.Count == 0) {
                return;
            }

            //平台最宽的一座做主岛，承载 MK2
            int mainIndex = 0;
            for (int i = 1; i < decks.Count; i++) {
                if (decks[i].PlateauHalf > decks[mainIndex].PlateauHalf) {
                    mainIndex = i;
                }
            }

            for (int i = 0; i < decks.Count; i++) {
                PopulateDeck(decks[i], i == mainIndex);
            }
        }

        private static bool TooCloseToOthers(List<int> placedXs, int centerX, int minSpacing) {
            foreach (int x in placedXs) {
                if (Math.Abs(x - centerX) < minSpacing) {
                    return true;
                }
            }
            return false;
        }

        //纯净天空判定：无液体，散块不超过 5 个（避开原版浮岛、云湖、活木树冠）
        private static bool AreaIsOpenSky(int startX, int startY, int width, int height) {
            int solidCount = 0;
            for (int x = startX; x < startX + width; x++) {
                for (int y = startY; y < startY + height; y++) {
                    if (!WorldGen.InWorld(x, y, 40)) {
                        return false;
                    }
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (tile.LiquidAmount > 0) {
                        return false;
                    }
                    if (tile.HasTile && ++solidCount > 5) {
                        return false;
                    }
                }
            }
            return true;
        }

        //平顶水滴形岛体：平台区锡镀层甲板，两肩泥土渐落，石芯偶带铜砖矿脉
        private static IslandDeck BuildIslandBody(int left, int deckY, int width, int height) {
            int centerX = left + width / 2;
            float halfW = width / 2f;
            int plateauHalf = (int)(halfW * 0.7f);

            for (int dx = 0; dx < width; dx++) {
                int x = left + dx;
                float nx = (dx - halfW + 0.5f) / halfW;//归一化 -1..1
                float absNx = Math.Abs(nx);

                //两肩逐级下沉 1~2 格，避免全平顶的人工感
                int topOffset = 0;
                if (absNx > 0.88f) {
                    topOffset = 2;
                }
                else if (absNx > 0.7f) {
                    topOffset = 1;
                }

                //椭圆下缘 + 噪声
                int depth = (int)(height * Math.Sqrt(Math.Max(0f, 1f - nx * nx)) * WorldGen.genRand.NextFloat(0.85f, 1.15f)) - topOffset;
                if (depth < 1) {
                    continue;
                }

                bool onPlateau = Math.Abs(x - centerX) <= plateauHalf;
                for (int dy = 0; dy < depth; dy++) {
                    int y = deckY + topOffset + dy;
                    if (!WorldGen.InWorld(x, y)) {
                        break;
                    }

                    ushort type;
                    if (dy <= 1) {
                        type = onPlateau ? TileID.TinPlating : TileID.Dirt;
                    }
                    else if (dy <= 3) {
                        type = TileID.Dirt;
                    }
                    else {
                        type = WorldGen.genRand.NextBool(12) ? TileID.CopperBrick : TileID.Stone;
                    }

                    Tile tile = Main.tile[x, y];
                    tile.HasTile = true;
                    tile.TileType = type;
                    tile.Slope = SlopeType.Solid;
                    tile.IsHalfBlock = false;
                    tile.LiquidAmount = 0;
                    WorldGen.SquareTileFrame(x, y);
                }
            }

            //两肩裸土长草
            for (int x = left; x < left + width; x++) {
                for (int y = deckY; y <= deckY + 2; y++) {
                    Tile tile = Framing.GetTileSafely(x, y);
                    Tile above = Framing.GetTileSafely(x, y - 1);
                    if (tile.HasTile && tile.TileType == TileID.Dirt && !above.HasTile) {
                        tile.TileType = TileID.Grass;
                        WorldGen.SquareTileFrame(x, y);
                        break;
                    }
                }
            }

            //平台两端金砖警示角
            foreach (int side in (ReadOnlySpan<int>)[-1, 1]) {
                for (int k = 0; k < 2; k++) {
                    int x = centerX + side * (plateauHalf - k);
                    for (int dy = 0; dy < 2; dy++) {
                        Tile tile = Framing.GetTileSafely(x, deckY + dy);
                        if (tile.HasTile && tile.TileType == TileID.TinPlating) {
                            tile.TileType = TileID.GoldBrick;
                            WorldGen.SquareTileFrame(x, deckY + dy);
                        }
                    }
                }
            }

            //岛底垂链
            int chainCount = WorldGen.genRand.Next(1, 4);
            for (int i = 0; i < chainCount; i++) {
                int x = centerX + WorldGen.genRand.Next(-plateauHalf + 2, plateauHalf - 1);
                int bottomY = FindColumnBottom(x, deckY, deckY + height + 4);
                if (bottomY < 0) {
                    continue;
                }
                int length = WorldGen.genRand.Next(3, 9);
                for (int k = 1; k <= length; k++) {
                    int y = bottomY + k;
                    if (!WorldGen.InWorld(x, y)) {
                        break;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile) {
                        break;
                    }
                    tile.HasTile = true;
                    tile.TileType = TileID.Chain;
                    WorldGen.SquareTileFrame(x, y);
                }
            }

            return new IslandDeck(centerX, deckY, plateauHalf, height);
        }

        //自上而下找该列最后一个实心格，找不到返回 -1
        private static int FindColumnBottom(int x, int fromY, int toY) {
            int bottom = -1;
            for (int y = fromY; y <= toY; y++) {
                if (Framing.GetTileSafely(x, y).HasTile) {
                    bottom = y;
                }
            }
            return bottom;
        }

        //甲板设施布置：风机、路灯、战利品木桶；主岛额外 MK2 + 管线垂柱
        private static void PopulateDeck(IslandDeck deck, bool isMain) {
            List<(int from, int to)> used = [];

            bool TryReserve(int from, int to) {
                if (from < deck.CenterX - deck.PlateauHalf + 1 || to > deck.CenterX + deck.PlateauHalf - 1) {
                    return false;
                }
                foreach (var (f, t) in used) {
                    if (from <= t + 1 && to >= f - 1) {
                        return false;//与已有设施相邻或重叠，留 1 格间隙
                    }
                }
                used.Add((from, to));
                return true;
            }

            int wildernessType = ModContent.TileType<WGGWildernessTile>();

            if (isMain) {
                //MK2 居中，垫层即甲板本体
                if (TryReserve(deck.CenterX - 2, deck.CenterX + 2)) {
                    WorldGen.PlaceTile(deck.CenterX, deck.DeckY - 1, ModContent.TileType<WGGMK2WildernessTile>(), mute: true);
                    PlacePipelineColumn(deck);
                }
                //平台够宽时侧边补一台荒野风机
                if (deck.PlateauHalf >= 13) {
                    int side = WorldGen.genRand.NextBool() ? -1 : 1;
                    int tx = deck.CenterX + side * (deck.PlateauHalf - 5);
                    if (TryReserve(tx - 1, tx + 1)) {
                        WorldGen.PlaceTile(tx, deck.DeckY - 1, wildernessType, mute: true);
                    }
                }
            }
            else {
                if (deck.PlateauHalf >= 12) {
                    //双机对置
                    foreach (int side in (ReadOnlySpan<int>)[-1, 1]) {
                        int tx = deck.CenterX + side * (deck.PlateauHalf - 5);
                        if (TryReserve(tx - 1, tx + 1)) {
                            WorldGen.PlaceTile(tx, deck.DeckY - 1, wildernessType, mute: true);
                        }
                    }
                }
                else {
                    int tx = deck.CenterX + WorldGen.genRand.Next(-2, 3);
                    if (TryReserve(tx - 1, tx + 1)) {
                        WorldGen.PlaceTile(tx, deck.DeckY - 1, wildernessType, mute: true);
                    }
                }
            }

            //路灯找空位
            if (TryFindFreeSpot(deck, used, 1, out int lampX)) {
                used.Add((lampX, lampX));
                PlaceLamppost(lampX, deck.DeckY);
            }

            //约半数岛放木桶，主岛必放
            if (isMain || WorldGen.genRand.NextBool(2)) {
                if (TryFindFreeSpot(deck, used, 2, out int barrelX)) {
                    used.Add((barrelX, barrelX + 1));
                    PlaceLootBarrel(barrelX, deck.DeckY);
                }
            }
        }

        //从平台边缘向内找一段宽 spotWidth 的空闲甲板
        private static bool TryFindFreeSpot(IslandDeck deck, List<(int from, int to)> used, int spotWidth, out int resultX) {
            for (int offset = deck.PlateauHalf - 2; offset >= 2; offset--) {
                foreach (int side in (ReadOnlySpan<int>)[-1, 1]) {
                    int from = deck.CenterX + side * offset;
                    int to = from + spotWidth - 1;
                    if (to > deck.CenterX + deck.PlateauHalf - 1) {
                        continue;
                    }

                    bool blocked = false;
                    foreach (var (f, t) in used) {
                        if (from <= t + 1 && to >= f - 1) {
                            blocked = true;
                            break;
                        }
                    }
                    if (blocked) {
                        continue;
                    }

                    resultX = from;
                    return true;
                }
            }
            resultX = 0;
            return false;
        }

        //主岛管线垂柱：自垫层下穿岛体，穿出岛腹再垂若干格，复刻灾厄版的工业垂管轮廓
        private static void PlacePipelineColumn(IslandDeck deck) {
            int pipeType = ModContent.TileType<UEPipelineTile>();
            int pipeX = deck.CenterX - 3;
            int bottomY = FindColumnBottom(pipeX, deck.DeckY, deck.DeckY + deck.BodyHeight + 4);
            if (bottomY < 0) {
                bottomY = deck.DeckY + 2;
            }
            int pipeBottom = bottomY + WorldGen.genRand.Next(6, 11);

            for (int y = deck.DeckY + 2; y <= pipeBottom; y++) {
                if (!WorldGen.InWorld(pipeX, y)) {
                    break;
                }
                if (Main.tile[pipeX, y].HasTile) {
                    WorldGen.KillTile(pipeX, y, noItem: true);
                }
                WorldGen.PlaceTile(pipeX, y, pipeType, mute: true);
            }
        }

        //路灯 1x6，帧值直写（frame-important，布局与 JunkmanBase 结构数据一致：FX=0，FY=行×18）
        private static void PlaceLamppost(int x, int deckY) {
            for (int k = 0; k < 6; k++) {
                int y = deckY - 6 + k;
                if (!WorldGen.InWorld(x, y)) {
                    return;
                }
                Tile tile = Main.tile[x, y];
                if (tile.HasTile) {
                    return;
                }
                tile.HasTile = true;
                tile.TileType = TileID.Lampposts;
                tile.TileFrameX = 0;
                tile.TileFrameY = (short)(k * 18);
            }
        }

        //木桶战利品：原版工业junk，呼应 JunkmanBase 战利品池
        private static void PlaceLootBarrel(int x, int deckY) {
            int chestIndex = WorldGen.PlaceChest(x, deckY - 1, TileID.Containers, notNearOtherChests: false, style: 5);
            if (chestIndex < 0) {
                return;
            }

            Chest chest = Main.chest[chestIndex];
            int slot = 0;
            chest.item[slot++] = new Item(ItemID.Wire, WorldGen.genRand.Next(8, 21));
            chest.item[slot++] = new Item(ItemID.Chain, WorldGen.genRand.Next(5, 16));
            chest.item[slot++] = new Item(ItemID.TinPlating, WorldGen.genRand.Next(10, 31));

            int[] junkItems = [
                ItemID.Glowstick, ItemID.Rope, ItemID.LesserHealingPotion,
                ItemID.Torch, ItemID.Bottle, ItemID.TinCan, ItemID.OldShoe
            ];
            int extraCount = WorldGen.genRand.Next(3, 7);
            for (int i = 0; i < extraCount && slot < chest.item.Length; i++) {
                chest.item[slot++] = new Item(junkItems[WorldGen.genRand.Next(junkItems.Length)], WorldGen.genRand.Next(1, 6));
            }
        }
    }
}
