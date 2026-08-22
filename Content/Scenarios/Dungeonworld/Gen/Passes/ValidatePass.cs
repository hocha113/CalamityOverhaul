using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ObjectData;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Passes
{
    //P80:帧修+玩家包络洪泛断言+计数报告(§3.4)
    //校验是断言不是修补,失败硬错误日志,只有帧修是必然执行的收尾(§3.1)
    //2000x6000=1200万tile规模评估:BFS只扩散凿空区(~10万节点)无碍;
    //耗时大头是全图RangeFrame与包络全扫(实心格首格即短路),预期秒级到十几秒,
    //visited布尔矩阵12MB一次性分配,GenReport的frameMs/bfsMs是实测回归基线
    internal class ValidatePass : GenPass
    {
        public ValidatePass() : base("Dungeonworld Validate", 2f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "校验连通性与帧修...";
            var log = CWRMod.Instance.Logger;
            var watch = Stopwatch.StartNew();

            //帧修,直写tile后的必然收尾(F25先例)
            Rectangle bounds = TileBrush.WrittenBounds;
            if (bounds.Width > 0) {
                WorldGen.RangeFrame(bounds.Left, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);
            }
            long frameMs = watch.ElapsedMilliseconds;
            progress.Set(0.4);

            //可通行=空气或平台;M0无门无液体
            //包络洪泛:2宽3高全可通行才算一个可站位,防"几何连了但玩家钻不过"
            watch.Restart();
            int width = Main.maxTilesX;
            int height = Main.maxTilesY;

            long envelopeTotal = 0;
            for (int x = 1; x < width - 2; x++) {
                for (int y = 1; y < height - 3; y++) {
                    if (EnvelopeFits(x, y)) {
                        envelopeTotal++;
                    }
                }
            }
            progress.Set(0.7);

            //出生姿态包络:脚踩spawnTileY顶,身体占其上3行2列
            int startX = Main.spawnTileX - 1;
            int startY = Main.spawnTileY - 3;
            bool[,] visited = new bool[width, height];
            long visitedCount = 0;

            if (!EnvelopeFits(startX, startY)) {
                log.Error($"[Dungeonworld] P80 出生点包络不可站({startX},{startY}),责任pass=P20");
            }
            else {
                var queue = new Queue<(int x, int y)>(1 << 12);
                visited[startX, startY] = true;
                visitedCount = 1;
                queue.Enqueue((startX, startY));
                while (queue.Count > 0) {
                    (int cx, int cy) = queue.Dequeue();
                    TryVisit(cx + 1, cy);
                    TryVisit(cx - 1, cy);
                    TryVisit(cx, cy + 1);
                    TryVisit(cx, cy - 1);
                }

                void TryVisit(int x, int y) {
                    if (x < 1 || y < 1 || x >= width - 2 || y >= height - 3 || visited[x, y]) {
                        return;
                    }
                    if (!EnvelopeFits(x, y)) {
                        return;
                    }
                    visited[x, y] = true;
                    visitedCount++;
                    queue.Enqueue((x, y));
                }
            }
            long bfsMs = watch.ElapsedMilliseconds;
            progress.Set(0.9);

            //断言:每层脊走廊中段(出生点正下方一列窗口)可达
            var bandResults = new StringBuilder();
            foreach (LayerBand band in DungeonworldMetrics.Bands) {
                bool reached = false;
                for (int x = DungeonworldMetrics.SpawnX - 10; x <= DungeonworldMetrics.SpawnX + 10 && !reached; x++) {
                    for (int y = band.SpineInteriorTop; y <= band.SpineFloorTop - 3 && !reached; y++) {
                        reached = visited[x, y];
                    }
                }
                if (!reached) {
                    log.Error($"[Dungeonworld] P80 {band.Name}脊走廊不可达(采样x={DungeonworldMetrics.SpawnX}±10),责任pass=P20");
                }
                if (bandResults.Length > 0) {
                    bandResults.Append(',');
                }
                bandResults.Append(reached ? "OK" : "FAIL");
            }

            //断言:深牢禁室内膛可达(内联跨脊落位,左右门即脊走廊,见GaolBossRoomSiting)
            string bossReport = "none";
            if (GaolBossRoomSiting.LastOrigin is Point bossOrigin) {
                bool bossReached = false;
                int floorRow = bossOrigin.Y + BossRooms.GaolBossRoom.LeftDoorOffset.Y + BossRooms.GaolBossRoom.DoorHeight;
                for (int x = bossOrigin.X + 3; x < bossOrigin.X + BossRooms.GaolBossRoom.Width - 4 && !bossReached; x++) {
                    for (int y = floorRow - 4; y < floorRow && !bossReached; y++) {
                        bossReached = visited[x, y];
                    }
                }
                if (!bossReached) {
                    log.Error($"[Dungeonworld] P80 深牢禁室内膛不可达 origin={bossOrigin},责任=P45落位/门槽对接");
                }
                bossReport = $"({bossOrigin.X},{bossOrigin.Y}){(bossReached ? "OK" : "FAIL")}";
            }

            //===P80审计扩展(Wave-1):门可开/两侧可站(§3.2-2)+家具锚定重验(§3.2-1)===
            //家具审计是"理论冗余的回归断言":装修全走PlaceObject,这里抓的是
            //"后续pass挖了家具脚下地板"类时序bug;逐锚定格重验,悬空=硬错误
            watch.Restart();
            int doorTotal = 0, doorFail = 0;
            long anchorCells = 0, anchorFail = 0;
            for (int x = 1; x < width - 1; x++) {
                for (int y = 1; y < height - 1; y++) {
                    Tile t = Main.tile[x, y];
                    if (!t.HasTile) {
                        continue;
                    }
                    ushort type = t.TileType;
                    //关门(1x3,F4/F5):frameY%54==0即门顶格,一门一验
                    if (type == TileID.ClosedDoor && t.TileFrameY % 54 == 0) {
                        doorTotal++;
                        bool openL = !Main.tile[x - 1, y].HasTile && !Main.tile[x - 1, y + 1].HasTile
                            && !Main.tile[x - 1, y + 2].HasTile;
                        bool openR = !Main.tile[x + 1, y].HasTile && !Main.tile[x + 1, y + 1].HasTile
                            && !Main.tile[x + 1, y + 2].HasTile;
                        bool standL = Passable(x - 1, y + 2);
                        bool standR = Passable(x + 1, y + 2);
                        if (!(openL || openR) || !standL || !standR) {
                            doorFail++;
                            log.Error($"[Dungeonworld] P80 DoorAudit 门({x},{y})"
                                + $" 可开L/R={openL}/{openR} 可站L/R={standL}/{standR},责任=P50装修");
                        }
                    }
                    if (!Main.tileFrameImportant[type]) {
                        continue;
                    }
                    TileObjectData data = TileObjectData.GetTileData(t);
                    if (data == null) {
                        continue;
                    }
                    int col = t.TileFrameX % data.CoordinateFullWidth
                        / (data.CoordinateWidth + data.CoordinatePadding);
                    int row = RowOf(t.TileFrameY % data.CoordinateFullHeight, data);
                    if (data.AnchorBottom.tileCount > 0 && row == data.Height - 1
                        && col >= data.AnchorBottom.checkStart
                        && col < data.AnchorBottom.checkStart + data.AnchorBottom.tileCount) {
                        anchorCells++;
                        if (!Main.tile[x, y + 1].HasTile) {
                            anchorFail++;
                            log.Error($"[Dungeonworld] P80 FurnitureAudit 底锚悬空 tile{type}@({x},{y})");
                        }
                    }
                    if (data.AnchorTop.tileCount > 0 && row == 0
                        && col >= data.AnchorTop.checkStart
                        && col < data.AnchorTop.checkStart + data.AnchorTop.tileCount) {
                        anchorCells++;
                        if (!Main.tile[x, y - 1].HasTile) {
                            anchorFail++;
                            log.Error($"[Dungeonworld] P80 FurnitureAudit 顶锚悬空 tile{type}@({x},{y})");
                        }
                    }
                }
            }
            //===死区审计:玩家钳制线外(x<PlayLeft或≥PlayRight)必须保持骨架实心===
            //原版BordersMovement把玩家挡在离左右边缘约41格外,这里出现任何可通行格
            //或液体都是"地图上看得见却永远走不到"的废几何,责任=越界写入的pass
            long edgeDeadBad = 0;
            for (int x = 0; x < width; x++) {
                if (x == DungeonworldMetrics.PlayLeft) {
                    x = DungeonworldMetrics.PlayRight;
                }
                for (int y = 0; y < height; y++) {
                    if (Passable(x, y) || Main.tile[x, y].LiquidAmount > 0) {
                        edgeDeadBad++;
                    }
                }
            }
            if (edgeDeadBad > 0) {
                log.Error($"[Dungeonworld] P80 边缘死区{edgeDeadBad}格可通行/含液体,玩家永远到不了,责任=越界写入的pass");
            }
            long auditMs = watch.ElapsedMilliseconds;

            //===密度预算指标(§3.5"防实心大陆"):挖空率/沿脊最大空白段/节点数===
            //两档制:硬线只在预算表HardEnabled带上fail loud(Wave-1=L1/L2),
            //其余带report-only;三条硬线为保守临时值,待首次QA按本报告数值回填校准
            watch.Restart();
            var density = new StringBuilder();
            for (int i = 0; i < DungeonworldMetrics.Bands.Length; i++) {
                LayerBand band = DungeonworldMetrics.Bands[i];
                DensityBudget budget = DensityBudgets.ByBand[i];
                long passableCells = 0;
                int maxBlank = 0, blank = 0;
                for (int x = DungeonworldMetrics.PlayLeft; x < DungeonworldMetrics.PlayRight; x++) {
                    //列开口=脊地板为可通行(平台/下探口)或脊顶以上有通行格(房/坡道/井/竖井)
                    bool open = Passable(x, band.SpineFloorTop);
                    for (int y = band.Top; y < band.Bottom; y++) {
                        if (Passable(x, y)) {
                            passableCells++;
                            if (y < band.SpineInteriorTop) {
                                open = true;
                            }
                        }
                    }
                    if (open) {
                        blank = 0;
                    }
                    else if (++blank > maxBlank) {
                        maxBlank = blank;
                    }
                }
                long bandArea = (long)(DungeonworldMetrics.PlayRight - DungeonworldMetrics.PlayLeft) * (band.Bottom - band.Top);
                double carve = passableCells * 100.0 / bandArea;
                int nodes = NodeCount(i);
                if (budget.HardEnabled) {
                    if (nodes < budget.MinNodes) {
                        log.Error($"[Dungeonworld] P80 密度闸 {band.Name} 节点数{nodes}<硬线{budget.MinNodes}"
                            + $"(目标{budget.NodeTarget}),责任=P50层内容");
                    }
                    if (maxBlank > budget.MaxBlankRun) {
                        log.Error($"[Dungeonworld] P80 密度闸 {band.Name} 脊空白段{maxBlank}>硬线{budget.MaxBlankRun},责任=P50层内容布局");
                    }
                    if (carve < budget.MinCarvePercent) {
                        log.Error($"[Dungeonworld] P80 密度闸 {band.Name} 挖空率{carve:F1}%<硬线{budget.MinCarvePercent}%"
                            + $"(理想{budget.CarveIdealPercent}%),责任=P50层内容");
                    }
                }
                if (density.Length > 0) {
                    density.Append(" | ");
                }
                density.Append($"{band.Name} carve={carve:F1}% blank={maxBlank} nodes={nodes}");
            }
            long densityMs = watch.ElapsedMilliseconds;

            double coverage = envelopeTotal > 0 ? visitedCount * 100.0 / envelopeTotal : 0.0;
            if (coverage < 95.0) {
                log.Warn($"[Dungeonworld] P80 洪泛覆盖率{coverage:F1}%<95%,存在出生点不可达的可通行区");
            }

            //一行结构化报告,多种子回归比对基线(§3.1-4)
            log.Info($"[Dungeonworld] GenReport seed={Main.ActiveWorldFileData?.SeedText}"
                + $" size={width}x{height}"
                + $" solid={TileBrush.SolidWrites} carve={TileBrush.ClearWrites} plat={TileBrush.PlatformWrites}"
                + $" envelopes={envelopeTotal} visited={visitedCount} coverage={coverage:F1}%"
                + $" bands={bandResults} bossRoom={bossReport}"
                //隔离带楼梯井位(Wave-2第二通道族),与P20日志互证
                + $" wells=[{VerticalLinks.Summary()}]"
                + $" doorAudit={doorFail}/{doorTotal} furnAudit={anchorFail}/{anchorCells} edgeDead={edgeDeadBad}"
                + $" scatter={ScatterEngine.TotalPlaced}/{ScatterEngine.TotalAttempts}"
                //填充体系自报增量:本世界从没跑出过"填充前"的基线,把两个pass各自
                //新增的凿空量单独记一格,单次运行就能读出"没有填充会是什么样"
                + $" infill[夹层{Infill.IntersticePass.CarveWrites}格 {Infill.IntersticePass.LastSummary}"
                + $" | 副翼{Infill.AnnexPass.CarveWrites}格 {Infill.AnnexPass.LastSummary}]"
                + $" downedBoss3={NPC.downedBoss3} hardMode={Main.hardMode}"
                //地狱判定线与最深可达行的余量在册,阈值排查结论见DungeonworldMetrics头注释
                + $" underworldLayer={Main.UnderworldLayer} deepestFloor={DungeonworldMetrics.Bands[^1].SpineFloorTop}"
                + $" frameMs={frameMs} bfsMs={bfsMs} auditMs={auditMs} densityMs={densityMs}"
                //密度指标组:硬线回填依据(STRUCTURES §3.5),多种子回归比对项
                + $" density[{density}]"
                + $" times[{GenClock.Summary()}]");
            progress.Set(1.0);
        }

        //层节点数:图内房间+不入图的名义大节点(L1教堂主体prefab/L2深牢禁室)
        //教堂失败会在L1入口fail loud抛出,走到这里即已落成
        //Wave-2:L3~L7走通用分支读各自ctx.Graph(P30已建七带上下文);
        //该层若日后出现不入图的演出型大节点(如L7倒吊中殿),随层稳定在此加名义计数
        private static int NodeCount(int bandIndex) => bandIndex switch {
            0 => (LayerPlans.L1?.Graph.Rooms.Count ?? 0) + 1,
            1 => (LayerPlans.L2?.Graph.Rooms.Count ?? 0)
                + (GaolBossRoomSiting.LastOrigin.HasValue ? 1 : 0),
            _ => LayerPlans.ByIndex(bandIndex)?.Graph.Rooms.Count ?? 0,
        };

        //帧内行定位:逐行累加CoordinateHeights+Padding,兼容非16高行(如书架底行)
        private static int RowOf(int frameYInStyle, TileObjectData data) {
            int acc = 0;
            for (int row = 0; row < data.Height; row++) {
                acc += data.CoordinateHeights[row] + data.CoordinatePadding;
                if (frameYInStyle < acc) {
                    return row;
                }
            }
            return data.Height - 1;
        }

        //可通行体素(§3.4):空气/平台/门，关门玩家可开(原版语义),开门本就通行
        //M0无门时代只认空气+平台,L1路建议采纳后补齐门语义(安全房/忏悔室室内不再折损覆盖率)
        private static bool Passable(int x, int y) {
            Tile tile = Main.tile[x, y];
            return !tile.HasTile
                || tile.TileType == TileID.Platforms
                || tile.TileType == TileID.ClosedDoor
                || tile.TileType == TileID.OpenDoor;
        }

        private static bool EnvelopeFits(int x, int y) {
            for (int dx = 0; dx < 2; dx++) {
                for (int dy = 0; dy < 3; dy++) {
                    if (!Passable(x + dx, y + dy)) {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
