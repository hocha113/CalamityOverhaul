using CalamityOverhaul.Content.Scenarios.Hadalworld.Gen.Core;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Terraria;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Gen.Passes
{
    //P80:帧修+2x3包络洪泛断言+水密/深度/死区审计+一行GenReport
    //校验是断言不是修补(镜像Dungeonworld ValidatePass哲学):失败即Error日志
    internal class HadalValidatePass : GenPass
    {
        public HadalValidatePass() : base("Hadalworld Validate", 2f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "洪泛校验与帧修...";
            var log = CWRMod.Instance.Logger;
            HadalTerrainModel model = HadalGenContext.Model;
            HadalTerrainPlan plan = model.Plan;
            int width = Main.maxTilesX;
            int height = Main.maxTilesY;
            int playLeft = model.P.PlayLeft;
            int playRight = model.P.PlayRight;

            //帧修:直写tile后的必然收尾(H8)
            var watch = Stopwatch.StartNew();
            WorldGen.RangeFrame(0, 0, width - 1, height - 1);
            long frameMs = watch.ElapsedMilliseconds;
            progress.Set(0.35);

            //包络洪泛:2宽3高全可通行才算可站位(H1),源=出生点站姿
            watch.Restart();
            var visited = new bool[width * height];
            long envelopeTotal = 0;
            for (int y = 60; y < height - 3; y++) {
                for (int x = 1; x < width - 2; x++) {
                    if (EnvelopeFits(x, y)) {
                        envelopeTotal++;
                    }
                }
            }
            long visitedCount = 0;
            int startX = Main.spawnTileX - 1;
            int startY = Main.spawnTileY - 3;
            bool spawnOk = EnvelopeFits(startX, startY);
            if (!spawnOk) {
                log.Error($"[Hadalworld] P80 出生点包络不可站({startX},{startY}),责任=P20出生房");
            }
            else {
                var queue = new Queue<(int x, int y)>(1 << 14);
                visited[startY * width + startX] = true;
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
                    if (x < 1 || y < 60 || x >= width - 2 || y >= height - 3 || visited[y * width + x]) {
                        return;
                    }
                    if (!EnvelopeFits(x, y)) {
                        return;
                    }
                    visited[y * width + x] = true;
                    visitedCount++;
                    queue.Enqueue((x, y));
                }
            }
            long bfsMs = watch.ElapsedMilliseconds;
            progress.Set(0.65);

            //主通路航点断言(蓝图§1.3不变量),逐站采样
            watch.Restart();
            var wayReport = new StringBuilder();
            void Way(string name, float fx, float fy, int radius) {
                bool ok = false;
                int cx = (int)fx, cy = (int)fy;
                for (int dy = -radius; dy <= radius && !ok; dy++) {
                    for (int dx = -radius; dx <= radius && !ok; dx++) {
                        int x = cx + dx, y = cy + dy;
                        ok = x > 0 && y > 0 && x < width && y < height && visited[y * width + x];
                    }
                }
                if (!ok) {
                    log.Error($"[Hadalworld] P80 航点[{name}]({cx},{cy})不可达,主通路断裂");
                }
                if (wayReport.Length > 0) {
                    wayReport.Append(',');
                }
                wayReport.Append(name).Append(ok ? "OK" : "FAIL");
            }
            float[] c = plan.CenterX;
            Way("沟口", c[210], 190f, 10);
            Way("暮光", c[900], 900f, 10);
            Way("午夜", c[2000], 2000f, 10);
            Way("门槛喉", c[2740], 2740f, 12);
            Way("平原", plan.Plain.CenterX, (plan.Plain.Top + plan.Plain.Bottom) * 0.5f, 30);
            if (plan.Shafts.Count > 0) {
                HadalPathNode mid = plan.Shafts[0].Nodes[plan.Shafts[0].Nodes.Count / 2];
                Way("主竖井", mid.X, mid.Y, 14);
            }
            if (plan.Halls.Count > 0) {
                Way("末厅", plan.Halls[^1].CX, plan.Halls[^1].CY, 20);
            }
            Way("V口", c[4150], 4150f, 12);
            Way("V底", c[4700], 4712f, 16);

            //封闭盆地反断言:登记死水应保持不可达,可达=密封破口
            int basinBreached = 0;
            foreach (HadalBasin basin in plan.Basins) {
                int bx = (int)basin.CX, by = (int)basin.CY;
                if (bx > 0 && by > 0 && bx < width && by < height && visited[by * width + bx]) {
                    basinBreached++;
                    log.Error($"[Hadalworld] P80 封闭盆地({bx},{by})被打穿,密封间距失效");
                }
            }

            //水密审计:海面下空格必有静水(登记气穴除外),气穴必干
            long wetBad = 0, dryBad = 0;
            for (int y = HadalworldMetrics.SeaLevelRow; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile) {
                        continue;
                    }
                    bool shouldAir = model.IsAirPocket(x, y);
                    if (shouldAir && tile.LiquidAmount > 0) {
                        dryBad++;
                    }
                    else if (!shouldAir && tile.LiquidAmount != byte.MaxValue) {
                        wetBad++;
                    }
                }
            }
            if (wetBad > 0 || dryBad > 0) {
                log.Error($"[Hadalworld] P80 水密审计失败:漏灌{wetBad}格,气穴进水{dryBad}格,责任=P20");
            }

            //深度审计:封底基岩带(≥4780)必须全实心,地狱判定线余量在册(Metrics裁决)
            long depthBad = 0;
            for (int y = HadalworldMetrics.DeepestPlayableRow; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile || tile.LiquidAmount > 0) {
                        depthBad++;
                    }
                }
            }
            if (depthBad > 0) {
                log.Error($"[Hadalworld] P80 深度审计失败:封底带{depthBad}格非实心/含液,责任=雕刻器守卫");
            }

            //边缘死区审计:玩家钳制线外海床以下必须实心(H2)
            long edgeBad = 0;
            for (int x = 0; x < width; x++) {
                if (x == playLeft) {
                    x = playRight;
                    if (x >= width) {
                        break;
                    }
                }
                for (int y = 300; y < height; y++) {
                    if (!Main.tile[x, y].HasTile) {
                        edgeBad++;
                    }
                }
            }
            if (edgeBad > 0) {
                log.Error($"[Hadalworld] P80 边缘死区{edgeBad}格可进入,玩家永远到不了,责任=越界开凿");
            }

            //窄喉实测宽度(演出锚复核,C路要用坐标)
            var chokeReport = new StringBuilder();
            foreach ((int cy, string name) in plan.Chokes) {
                int center = (int)c[cy];
                int gap = 0;
                for (int x = center - 90; x <= center + 90; x++) {
                    if (x > 0 && x < width && !Main.tile[x, cy].HasTile) {
                        gap++;
                    }
                }
                if (chokeReport.Length > 0) {
                    chokeReport.Append(',');
                }
                chokeReport.Append(name).Append('@').Append(cy).Append('w').Append(gap);
            }
            long auditMs = watch.ElapsedMilliseconds;

            double coverage = envelopeTotal > 0 ? visitedCount * 100.0 / envelopeTotal : 0.0;
            //登记盆地体积从告警语义中扣除:覆盖率含盆地,阈值放到97
            if (coverage < 97.0) {
                log.Warn($"[Hadalworld] P80 洪泛覆盖率{coverage:F1}%<97%,存在未登记的不可达腔");
            }

            log.Info($"[Hadalworld] GenReport seed={Main.ActiveWorldFileData?.SeedText}"
                + $" size={width}x{height}"
                + $" solid={HadalGenContext.SolidWrites} water={HadalGenContext.WaterWrites}"
                + $" air={HadalGenContext.AirWrites} walls={HadalGenContext.WallWrites}"
                + $" envelopes={envelopeTotal} visited={visitedCount} coverage={coverage:F1}%"
                + $" spawn=({Main.spawnTileX},{Main.spawnTileY}){(spawnOk ? "OK" : "FAIL")}"
                + $" way[{wayReport}]"
                + $" chokes[{chokeReport}]"
                + $" basins={plan.Basins.Count}(breach={basinBreached})"
                + $" wetBad={wetBad} dryBad={dryBad} depthBad={depthBad} edgeBad={edgeBad}"
                + $" decor[{HadalGenContext.DecorSummary()}]"
                + $" underworldLayer={Main.UnderworldLayer} deepestPlayable={HadalworldMetrics.DeepestPlayableRow}"
                + $" frameMs={frameMs} bfsMs={bfsMs} auditMs={auditMs}"
                + $" times[{HadalGenClock.Summary()}]");

            //模型体量~23MB,校验完即释放
            HadalGenContext.Model = null;
            progress.Set(1.0);
        }

        //可通行=无物块或非实心装饰(珊瑚/堆/钟乳石等均可游过)
        private static bool Passable(int x, int y) {
            Tile tile = Main.tile[x, y];
            return !tile.HasTile || !Main.tileSolid[tile.TileType];
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
