using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.VoidPortals.AbandonedPortals
{
    /// <summary>
    /// 废墟传送门选址：在出生点正下方的洞穴层中，优先寻找天然的开阔空间，
    /// 找到后只填补缺失的地基；当无法找到合适空间时，最后才挖掘一个生成位。
    /// </summary>
    internal static class AbandonedPortalSiteFinder
    {
        //搜索水平半径（tile）
        private const int SearchRadiusX = 220;
        //搜索深度（tile）
        private const int SearchMaxDepth = 1100;
        //每列尝试步长，避免计算过密
        private const int ColumnStep = 3;
        //每列内 Y 扫描步长
        private const int RowStep = 2;
        //允许的最低开阔度门槛（找到完美空间时）
        private const int RequiredOpenWidth = AbandonedPortalTile.Width + 4;
        private const int RequiredOpenHeight = AbandonedPortalTile.Height + 3;
        //地基缺口允许的最大百分比，找到大致平整的地面就视为合格
        private const float MaxFloorGapRatio = 0.35f;
        //安全边距
        private const int WorldEdgeMargin = 40;
        //准备生成位时，门体外缘留出的清理缓冲（tile）
        private const int ClearMarginTiles = 1;

        /// <summary>
        /// 公开入口：返回门体左上角的 tile 坐标。<br/>
        /// 三阶段：①找完美天然腔体 → ②在合理腔体中填补地基 → ③如全失败则挖掘
        /// </summary>
        internal static Point Resolve() {
            int spawnX = Math.Clamp(Main.spawnTileX, WorldEdgeMargin, Main.maxTilesX - WorldEdgeMargin);
            int rockTop = (int)Main.rockLayer + 30; //至少进入洞穴层 30 tile
            int searchTop = Math.Max(rockTop, Main.spawnTileY + 80);
            int searchBottom = Math.Min(Main.maxTilesY - 220, searchTop + SearchMaxDepth);

            //阶段 1：寻找完美天然腔体（开阔且地基平整）
            if (TryFindNaturalCavity(spawnX, searchTop, searchBottom, allowFloorPatch: false, out Point perfectSpot)) {
                return perfectSpot;
            }

            //阶段 2：放宽——允许底部存在一定缺口，进行少量地基填补
            if (TryFindNaturalCavity(spawnX, searchTop, searchBottom, allowFloorPatch: true, out Point patchSpot)) {
                return patchSpot;
            }

            //阶段 3：实在找不到，就在期望的位置直接挖一个洞
            int forcedY = Math.Min(searchBottom, searchTop + 200);
            int forcedX = Math.Clamp(spawnX - AbandonedPortalTile.Width / 2, WorldEdgeMargin,
                Main.maxTilesX - AbandonedPortalTile.Width - WorldEdgeMargin);
            int forcedTopY = Math.Clamp(forcedY - AbandonedPortalTile.Height, WorldEdgeMargin,
                Main.maxTilesY - AbandonedPortalTile.Height - WorldEdgeMargin);
            return new Point(forcedX, forcedTopY);
        }

        /// <summary>
        /// 在 spawnX 周围以螺旋顺序搜索符合条件的洞穴空间。
        /// </summary>
        private static bool TryFindNaturalCavity(int spawnX, int searchTop, int searchBottom, bool allowFloorPatch, out Point result) {
            //螺旋扫描：自中心向两侧逐步外扩，深度自上而下
            for (int dx = 0; dx <= SearchRadiusX; dx += ColumnStep) {
                for (int sign = -1; sign <= 1; sign += 2) {
                    if (dx == 0 && sign == 1) continue; //中心列只评估一次

                    int x = spawnX + dx * sign;
                    int leftTile = x - AbandonedPortalTile.Width / 2;
                    if (leftTile < WorldEdgeMargin || leftTile + AbandonedPortalTile.Width >= Main.maxTilesX - WorldEdgeMargin) {
                        continue;
                    }

                    for (int y = searchTop; y < searchBottom; y += RowStep) {
                        if (EvaluateBox(leftTile, y, allowFloorPatch)) {
                            //y 是地基行，门体顶部在其上方 TileHeight 行
                            result = new Point(leftTile, y - AbandonedPortalTile.Height);
                            return true;
                        }
                    }
                }
            }
            result = default;
            return false;
        }

        /// <summary>
        /// 评估候选位置：(leftTile, floorY) 表示候选地基行；<br/>
        /// 门体占据 floorY 上方 TileHeight 行，floorY 自身是要求的实心地基。
        /// </summary>
        private static bool EvaluateBox(int leftTile, int floorY, bool allowFloorPatch) {
            //门体顶部
            int topY = floorY - AbandonedPortalTile.Height;
            if (topY <= WorldEdgeMargin) return false;
            if (floorY + 4 >= Main.maxTilesY - WorldEdgeMargin) return false;

            //1) 上方开阔：评估比 Portal 略大的范围（开阔度更友好）
            int openLeft = leftTile - 2;
            int openRight = leftTile + AbandonedPortalTile.Width + 1;
            int openTop = topY - 1;
            int openBottom = floorY - 1;
            int openSampleCount = 0;
            int openClearCount = 0;
            for (int x = openLeft; x <= openRight; x++) {
                for (int y = openTop; y <= openBottom; y++) {
                    openSampleCount++;
                    Tile t = Framing.GetTileSafely(x, y);
                    if (!IsSolidObstruction(t) && t.LiquidAmount == 0) {
                        openClearCount++;
                    }
                }
            }
            float openRatio = openClearCount / (float)openSampleCount;
            //完美：>= 95% 空气；放宽：>= 80% 空气
            float openThreshold = allowFloorPatch ? 0.80f : 0.95f;
            if (openRatio < openThreshold) return false;

            //2) 地基：floorY 那一行至少要有大部分固体；下方再加一行做缓冲
            int floorSamples = 0;
            int floorSolids = 0;
            int floorLeft = leftTile - 1;
            int floorRight = leftTile + AbandonedPortalTile.Width;
            for (int x = floorLeft; x <= floorRight; x++) {
                Tile t1 = Framing.GetTileSafely(x, floorY);
                Tile t2 = Framing.GetTileSafely(x, floorY + 1);
                floorSamples += 2;
                if (IsSolidGround(t1)) floorSolids++;
                if (IsSolidGround(t2)) floorSolids++;
            }
            float floorRatio = floorSolids / (float)floorSamples;
            //完美：>= 90% 地基；放宽：>= (1 - MaxFloorGapRatio*2) 也即 30%（最多挖一半再补）
            float floorThreshold = allowFloorPatch ? 1f - MaxFloorGapRatio : 0.90f;
            if (floorRatio < floorThreshold) return false;

            //3) 不能有大液体堆积
            if (HasSignificantLiquid(leftTile, topY, AbandonedPortalTile.Width, AbandonedPortalTile.Height)) {
                return false;
            }

            //4) 必须达到最小开阔尺寸（防止位置卡在低矮缝里）
            if (!HasMinimumChamber(leftTile + AbandonedPortalTile.Width / 2, topY + AbandonedPortalTile.Height / 2,
                    RequiredOpenWidth, RequiredOpenHeight)) {
                if (!allowFloorPatch) return false;
            }

            return true;
        }

        private static bool IsSolidObstruction(Tile tile) {
            //只把"完整方块"视为阻挡，让平台、家具等可被忽略
            return tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }

        private static bool IsSolidGround(Tile tile) {
            //作为地基只接受完整石/泥/沙类方块，避免把灌木/草识别为支撑
            return tile.HasTile && Main.tileSolid[tile.TileType] && !TileID.Sets.Platforms[tile.TileType];
        }

        private static bool HasSignificantLiquid(int left, int top, int w, int h) {
            int liquid = 0;
            int total = 0;
            for (int x = left; x < left + w; x += 2) {
                for (int y = top; y < top + h; y += 2) {
                    total++;
                    if (Framing.GetTileSafely(x, y).LiquidAmount > 80) liquid++;
                }
            }
            return total > 0 && liquid * 5 > total; // > 20% 体积是液体则拒绝
        }

        /// <summary>
        /// 在中心点附近查找一个最少 reqW × reqH 的近似空腔。
        /// </summary>
        private static bool HasMinimumChamber(int cx, int cy, int reqW, int reqH) {
            int halfW = reqW / 2;
            int halfH = reqH / 2;
            int clearCount = 0;
            int total = 0;
            for (int x = cx - halfW; x <= cx + halfW; x++) {
                for (int y = cy - halfH; y <= cy + halfH; y++) {
                    total++;
                    if (!IsSolidObstruction(Framing.GetTileSafely(x, y))) clearCount++;
                }
            }
            return total > 0 && clearCount / (float)total >= 0.85f;
        }

        /// <summary>
        /// 准备生成位：①清空门体足迹 ②补齐地基 ③向外消除少量遮挡
        /// </summary>
        internal static void PreparePortalSite(int leftTile, int topTile) {
            int right = leftTile + AbandonedPortalTile.Width - 1;
            int bottom = topTile + AbandonedPortalTile.Height - 1;

            //1) 清空门体内部 + 一格缓冲，让 Actor 能完整可见
            ClearTiles(leftTile - ClearMarginTiles, topTile - ClearMarginTiles,
                right + ClearMarginTiles, bottom);

            //2) 补齐底部地基：把门下方两行内缺失的固体填上（仅限正下方与左右一格）
            FillFloor(leftTile - 1, right + 1, bottom + 1, bottom + 2);

            //3) 在底部边缘做轻量"风化清理"——把门两侧贴近门体的悬空块也敲掉，避免外观突兀
            CleanupLowerEdges(leftTile - ClearMarginTiles, right + ClearMarginTiles, bottom + 1);

            //广播一次大范围 TileSquare（多人同步用）
            if (Main.netMode == NetmodeID.Server) {
                int cx = (leftTile + right) / 2;
                int cy = (topTile + bottom) / 2;
                int size = Math.Max(right - leftTile + 4, bottom - topTile + 6);
                NetMessage.SendTileSquare(-1, cx, cy, size);
            }
        }

        private static void ClearTiles(int left, int top, int right, int bottom) {
            left = Math.Clamp(left, 1, Main.maxTilesX - 2);
            right = Math.Clamp(right, 1, Main.maxTilesX - 2);
            top = Math.Clamp(top, 1, Main.maxTilesY - 2);
            bottom = Math.Clamp(bottom, 1, Main.maxTilesY - 2);

            for (int x = left; x <= right; x++) {
                for (int y = top; y <= bottom; y++) {
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (!tile.HasTile) continue;
                    WorldGen.KillTile(x, y, fail: false, effectOnly: false, noItem: true);
                }
            }
        }

        private static void FillFloor(int left, int right, int top, int bottom) {
            left = Math.Clamp(left, 1, Main.maxTilesX - 2);
            right = Math.Clamp(right, 1, Main.maxTilesX - 2);
            top = Math.Clamp(top, 1, Main.maxTilesY - 2);
            bottom = Math.Clamp(bottom, 1, Main.maxTilesY - 2);

            //优先沿用周边主体物块作为填充类型，让填补尽量自然
            ushort fillType = ResolveSurroundingTileType(left, right, top);

            for (int x = left; x <= right; x++) {
                for (int y = top; y <= bottom; y++) {
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (tile.HasTile && Main.tileSolid[tile.TileType]) continue;
                    if (tile.LiquidAmount > 0) tile.LiquidAmount = 0;
                    WorldGen.PlaceTile(x, y, fillType, mute: true, forced: true);
                }
            }
        }

        private static void CleanupLowerEdges(int left, int right, int floorY) {
            //把门体足下两块外侧的"悬挂物"（草、藤蔓等）清掉，避免视觉穿模
            for (int x = left; x <= right; x++) {
                for (int dy = 0; dy <= 1; dy++) {
                    int y = floorY - dy;
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (!tile.HasTile) continue;
                    //只在不是稳定地基（非纯固体）的情况下处理，避免破坏地形
                    if (!Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]) {
                        WorldGen.KillTile(x, y, fail: false, effectOnly: false, noItem: true);
                    }
                }
            }
        }

        private static ushort ResolveSurroundingTileType(int left, int right, int floorY) {
            //在门体左右各扫几列，统计最常见的固体类型
            int[] counts = new int[TileLoader.TileCount];
            int sampleSpan = 6;
            int sampleX1 = Math.Max(1, left - sampleSpan);
            int sampleX2 = Math.Min(Main.maxTilesX - 2, right + sampleSpan);
            int sampleY1 = Math.Max(1, floorY);
            int sampleY2 = Math.Min(Main.maxTilesY - 2, floorY + 5);
            for (int x = sampleX1; x <= sampleX2; x++) {
                for (int y = sampleY1; y <= sampleY2; y++) {
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (!tile.HasTile) continue;
                    if (!Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]) continue;
                    if (tile.TileType >= counts.Length) continue;
                    counts[tile.TileType]++;
                }
            }

            int bestType = TileID.Stone;
            int bestCount = 0;
            for (int i = 0; i < counts.Length; i++) {
                if (counts[i] > bestCount) {
                    bestCount = counts[i];
                    bestType = i;
                }
            }
            return (ushort)bestType;
        }
    }
}
