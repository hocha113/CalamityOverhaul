using CalamityOverhaul.Content.Scenarios.OldNet.Tiles;
using System.Collections.Generic;
using Terraria;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Passes
{
    //P80 校验：连通性洪泛（锚点可达/封锁区不可达）+ 节点配额审计 +
    //全图 RangeFrame（本子世界任务表没有原版收尾帧修）+ GenReport
    //问题一律 log 报告不中断生成，fail loud but keep playable
    internal class OldNetValidatePass : GenPass
    {
        public OldNetValidatePass() : base("OldNet Validate", 0.4f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "校验旧网连通性...";
            bool[,] reachable = FloodFromSpawn();
            progress.Set(0.35);

            CheckAnchors(reachable);
            progress.Set(0.5);

            AuditNodes();
            progress.Set(0.5);

            AuditWalls();
            progress.Set(0.55);

            AuditSockets();
            progress.Set(0.6);

            progress.Message = "校准数据平原帧序...";
            WorldGen.RangeFrame(0, 0, Main.maxTilesX - 1, Main.maxTilesY - 1);
            progress.Set(1.0);

            CWRMod.Instance.Logger.Info(
                $"[OldNet] GenReport clock[{OldNetGenClock.Summary()}]"
                + $" brush[solid={OldNetTileBrush.SolidWrites} clear={OldNetTileBrush.ClearWrites}"
                + $" platform={OldNetTileBrush.PlatformWrites}]"
                + GridStats());
        }

        //空气连通洪泛（4邻域）：平台/非实心可通过。air连通≠可走，
        //但井内歇脚平台已保证竖向可通行，洪泛抓的是"封死/断头"级灾难
        private static bool[,] FloodFromSpawn() {
            int width = Main.maxTilesX;
            int height = Main.maxTilesY;
            var reachable = new bool[width, height];
            var queue = new Queue<Point>();
            var start = new Point(Main.spawnTileX, Main.spawnTileY - 2);
            if (!Passable(start.X, start.Y)) {
                CWRMod.Instance.Logger.Warn($"[OldNet] 校验：出生点不可站立@{start}");
                return reachable;
            }
            reachable[start.X, start.Y] = true;
            queue.Enqueue(start);
            while (queue.Count > 0) {
                Point p = queue.Dequeue();
                Visit(p.X + 1, p.Y);
                Visit(p.X - 1, p.Y);
                Visit(p.X, p.Y + 1);
                Visit(p.X, p.Y - 1);
            }
            return reachable;

            void Visit(int x, int y) {
                if (x < 0 || y < 0 || x >= width || y >= height
                    || reachable[x, y] || !Passable(x, y)) {
                    return;
                }
                reachable[x, y] = true;
                queue.Enqueue(new Point(x, y));
            }
        }

        private static bool Passable(int x, int y) {
            Tile tile = Main.tile[x, y];
            return !tile.HasTile || !Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType];
        }

        private static void CheckAnchors(bool[,] reachable) {
            //登出终端可达
            int terminalType = ModContent.TileType<OldNetLogoutTerminalTile>();
            bool terminalOk = ScanReach(reachable, terminalType,
                OldNetMetrics.LogoutX - 2, OldNetMetrics.LogoutX + 3,
                OldNetMetrics.FloorRow - 30, OldNetMetrics.FloorRow + 4);
            if (!terminalOk) {
                CWRMod.Instance.Logger.Warn("[OldNet] 校验：登出终端不可达或缺失");
            }

            //中继可达
            int relayType = ModContent.TileType<OldNetRelayTile>();
            int relayOk = 0;
            foreach (Point spot in OldNetPlans.RelaySpots) {
                if (ScanReach(reachable, relayType, spot.X - 2, spot.X + 14,
                    spot.Y - 6, spot.Y + 2)) {
                    relayOk++;
                }
            }
            if (relayOk < OldNetPlans.RelaySpots.Count) {
                CWRMod.Instance.Logger.Warn(
                    $"[OldNet] 校验：中继可达 {relayOk}/{OldNetPlans.RelaySpots.Count}");
            }

            //竖井平台厅可达（地下骨架贯通）
            int landingOk = 0;
            foreach (OldNetShaft shaft in OldNetPlans.Shafts) {
                Point c = shaft.Landing.Center;
                if (reachable[c.X, c.Y]) {
                    landingOk++;
                }
            }
            if (landingOk < OldNetPlans.Shafts.Count) {
                CWRMod.Instance.Logger.Warn(
                    $"[OldNet] 校验：平台厅可达 {landingOk}/{OldNetPlans.Shafts.Count}");
            }

            //封锁区内腔必须不可达（闸门完好）
            foreach (Rectangle box in OldNetPlans.SealBoxes) {
                Point c = box.Center;
                if (reachable[c.X, c.Y]) {
                    CWRMod.Instance.Logger.Warn($"[OldNet] 校验：封锁区漏气@{box}");
                }
            }

            CWRMod.Instance.Logger.Info(
                $"[OldNet] 校验 terminal={terminalOk} relays={relayOk}/{OldNetPlans.RelaySpots.Count}"
                + $" landings={landingOk}/{OldNetPlans.Shafts.Count}");
        }

        //区域内找目标tile并确认其格可达（节点/终端tile非实心，格本身即可达判定点）
        private static bool ScanReach(bool[,] reachable, int type, int x0, int x1, int y0, int y1) {
            for (int x = x0; x < x1; x++) {
                for (int y = y0; y < y1; y++) {
                    if (!WorldGen.InWorld(x, y)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && tile.TileType == type && reachable[x, y]) {
                        return true;
                    }
                }
            }
            return false;
        }

        //全图节点计数 vs 配额账本
        private static void AuditNodes() {
            int plainType = ModContent.TileType<OldNetDataNodeTile>();
            int encryptType = ModContent.TileType<OldNetEncryptedNodeTile>();
            int eventType = ModContent.TileType<OldNetEventNodeTile>();
            int plain = 0, encrypt = 0, evt = 0;
            for (int x = 0; x < Main.maxTilesX; x++) {
                for (int y = 0; y < Main.maxTilesY; y++) {
                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile) {
                        continue;
                    }
                    if (tile.TileType == plainType) {
                        plain++;
                    }
                    else if (tile.TileType == encryptType) {
                        encrypt++;
                    }
                    else if (tile.TileType == eventType) {
                        evt++;
                    }
                }
            }
            OldNetNodeBudget budget = OldNetPlans.Budget;
            int expectPlain = budget.PlainPlaced + budget.UnderPlainPlaced;
            if (plain != expectPlain || evt != budget.EventPlaced) {
                CWRMod.Instance.Logger.Warn(
                    $"[OldNet] 配额审计偏差 plain={plain}/{expectPlain} event={evt}/{budget.EventPlaced}");
            }
            //加密节点世界计数含封锁区盒内（不入 Budget），只报不判
            CWRMod.Instance.Logger.Info(
                $"[OldNet] 节点审计 plain={plain} encrypt={encrypt}(含盒内) event={evt}");
        }

        //DoorSocket 审计：非平台厅房间零开口 = 密闭死房（建造方漏登记或漏凿门）。只报不断
        private static void AuditSockets() {
            int sealedRooms = 0;
            foreach (OldNetBuildContext ctx in new[] {
                OldNetPlans.Z1, OldNetPlans.Z2, OldNetPlans.Z3, OldNetPlans.Z4 }) {
                if (ctx == null) {
                    continue;
                }
                foreach (Rooms.OldNetRoomNode room in ctx.Graph.Rooms) {
                    if (room.Role != Rooms.OldNetRoomRole.Landing && room.Sockets.Count == 0) {
                        sealedRooms++;
                    }
                }
            }
            if (sealedRooms > 0) {
                CWRMod.Instance.Logger.Warn($"[OldNet] 校验：{sealedRooms} 间房零开口（密闭死房）");
            }
        }

        //P70 之后地表线以下不允许再有无墙格（无墙=透天幕）；
        //封锁区内腔在地表以上但属密闭盒，一并查。只报不断
        private static void AuditWalls() {
            int[] floorTop = OldNetPlans.FloorTop;
            int right = Main.maxTilesX - OldNetMetrics.BorderThick;
            int bottom = Main.maxTilesY - OldNetMetrics.BorderThick;
            int missing = 0;
            for (int x = OldNetMetrics.BorderThick; x < right; x++) {
                for (int y = floorTop[x]; y < bottom; y++) {
                    if (Main.tile[x, y].WallType == Terraria.ID.WallID.None) {
                        missing++;
                    }
                }
            }
            foreach (Rectangle box in OldNetPlans.SealBoxes) {
                for (int x = box.X + 1; x < box.Right - 1; x++) {
                    for (int y = box.Y + 1; y < box.Bottom; y++) {
                        Tile tile = Main.tile[x, y];
                        if (!tile.HasTile && tile.WallType == Terraria.ID.WallID.None) {
                            missing++;
                        }
                    }
                }
            }
            if (missing > 0) {
                CWRMod.Instance.Logger.Warn($"[OldNet] 校验：地下/封锁区存在无墙格 {missing} 处");
            }
            else {
                CWRMod.Instance.Logger.Info("[OldNet] 校验：地下墙体覆盖完整");
            }
        }

        private static string GridStats() {
            string Stat(OldNetBuildContext ctx) => ctx == null ? "-"
                : $"{ctx.Grid.ReserveOk}ok/{ctx.Grid.ReserveReject}rej";
            return $" grids[Z1={Stat(OldNetPlans.Z1)} Z2={Stat(OldNetPlans.Z2)}"
                + $" Z3={Stat(OldNetPlans.Z3)} Z4={Stat(OldNetPlans.Z4)}]";
        }
    }
}
