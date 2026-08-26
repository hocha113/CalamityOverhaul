using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Gen.Core
{
    //核心层统一入口:参数→规划→雕刻→模型(游戏侧与harness共用此门)
    internal static class HadalTerrain
    {
        internal static HadalTerrainModel Build(HadalGenParams p) {
            var rng = new HadalRng(p.Seed);
            HadalTerrainPlan plan = HadalTerrainPlanner.Build(p, rng);
            return HadalTerrainCarver.Carve(p, plan, rng);
        }
    }

    //材质栅格模型:核心层输出,游戏侧只做"读格→落物块"投影
    internal sealed class HadalTerrainModel
    {
        internal readonly HadalGenParams P;
        internal readonly HadalTerrainPlan Plan;
        internal readonly byte[] Mat; //y*W+x
        internal int SpawnX, SpawnY;
        //出生房内膛气穴矩形(含边界),此外全按海面规则灌水
        internal int AirL, AirT, AirR, AirB;
        internal long CarveOps, FillOps;

        private readonly int _w, _h;

        internal HadalTerrainModel(HadalGenParams p, HadalTerrainPlan plan) {
            P = p;
            Plan = plan;
            _w = p.Width;
            _h = p.Height;
            Mat = new byte[_w * _h];
            AirL = -1;
        }

        internal HadalMat At(int x, int y) {
            if (x < 0 || y < 0 || x >= _w || y >= _h) {
                return HadalMat.Stone; //越界视作实心
            }
            return (HadalMat)Mat[y * _w + x];
        }

        /// <summary>直填材质(基底/柱/丘/壳),只钳世界界</summary>
        internal void Fill(int x, int y, HadalMat mat) {
            if (x < 0 || y < 0 || x >= _w || y >= _h) {
                return;
            }
            Mat[y * _w + x] = (byte)mat;
            FillOps++;
        }

        /// <summary>开凿:统一守卫(钳制死区/封底/顶带,蓝图§6-5/6-6)</summary>
        internal void Carve(int x, int y) {
            if (x < P.PlayLeft + 2 || x >= P.PlayRight - 2
                || y < 60 || y >= P.DeepestPlayableRow - 6) {
                return;
            }
            Mat[y * _w + x] = (byte)HadalMat.None;
            CarveOps++;
        }

        internal void SetAirRect(int l, int t, int r, int b) {
            AirL = l;
            AirT = t;
            AirR = r;
            AirB = b;
        }

        internal bool IsAirPocket(int x, int y)
            => AirL >= 0 && x >= AirL && x <= AirR && y >= AirT && y <= AirB;

        /// <summary>该格最终形态是否为水(游戏侧/渲染共用规则)</summary>
        internal bool IsWater(int x, int y)
            => At(x, y) == HadalMat.None && y >= P.SeaLevelRow && !IsAirPocket(x, y);

        //——包络洪泛(2宽3高,H1):harness质量门与报告用;游戏侧P80对真tile再做一遍——
        //visited可外供(harness渲染可达性叠加),null则内部分配
        internal HadalFloodReport Flood(bool[] visited = null) {
            var rep = new HadalFloodReport();
            int w = _w, h = _h;
            visited ??= new bool[w * h];
            bool Passable(int x, int y) => x > 0 && y > 0 && x < w && y < h
                && Mat[y * w + x] == (byte)HadalMat.None;
            bool Fits(int x, int y) {
                for (int dx = 0; dx < 2; dx++) {
                    for (int dy = 0; dy < 3; dy++) {
                        if (!Passable(x + dx, y + dy)) {
                            return false;
                        }
                    }
                }
                return true;
            }

            long fitTotal = 0;
            for (int y = 60; y < h - 3; y++) {
                for (int x = 1; x < w - 2; x++) {
                    if (Fits(x, y)) {
                        fitTotal++;
                    }
                }
            }
            rep.EnvelopeTotal = fitTotal;

            int startX = SpawnX - 1, startY = SpawnY - 3;
            if (!Fits(startX, startY)) {
                rep.SpawnStandOk = false;
                return rep;
            }
            rep.SpawnStandOk = true;
            var queue = new Queue<(int, int)>(1 << 14);
            visited[startY * w + startX] = true;
            queue.Enqueue((startX, startY));
            long count = 1;
            while (queue.Count > 0) {
                (int cx, int cy) = queue.Dequeue();
                Try(cx + 1, cy);
                Try(cx - 1, cy);
                Try(cx, cy + 1);
                Try(cx, cy - 1);
                void Try(int x, int y) {
                    if (x < 1 || y < 60 || x >= w - 2 || y >= h - 3 || visited[y * w + x]) {
                        return;
                    }
                    if (!Fits(x, y)) {
                        return;
                    }
                    visited[y * w + x] = true;
                    count++;
                    queue.Enqueue((x, y));
                }
            }
            rep.Visited = count;

            //航点断言:主通路不变量逐站采样(蓝图§1.3)
            bool Reached(float fx, float fy, int radius) {
                int cx = (int)fx, cy = (int)fy;
                for (int dy = -radius; dy <= radius; dy++) {
                    for (int dx = -radius; dx <= radius; dx++) {
                        int x = cx + dx, y = cy + dy;
                        if (x > 0 && y > 0 && x < w && y < h && visited[y * w + x]) {
                            return true;
                        }
                    }
                }
                return false;
            }
            float[] c = Plan.CenterX;
            rep.Waypoints.Add(("沟口", Reached(c[210], 190f, 10)));
            rep.Waypoints.Add(("暮光沟", Reached(c[900], 900f, 10)));
            rep.Waypoints.Add(("午夜沟", Reached(c[2000], 2000f, 10)));
            rep.Waypoints.Add(("门槛喉", Reached(c[2740], 2740f, 12)));
            HadalPlainSpec plain = Plan.Plain;
            rep.Waypoints.Add(("平原", Reached(plain.CenterX, (plain.Top + plain.Bottom) * 0.5f, 30)));
            if (Plan.Shafts.Count > 0) {
                HadalPathNode mid = Plan.Shafts[0].Nodes[Plan.Shafts[0].Nodes.Count / 2];
                rep.Waypoints.Add(("主竖井", Reached(mid.X, mid.Y, 14)));
            }
            if (Plan.Halls.Count > 0) {
                HadalHall lastHall = Plan.Halls[^1];
                rep.Waypoints.Add(("末厅", Reached(lastHall.CX, lastHall.CY, 20)));
            }
            rep.Waypoints.Add(("V口", Reached(c[4150], 4150f, 12)));
            rep.Waypoints.Add(("V底", Reached(c[4700], 4712f, 16)));

            //封闭盆地应不可达(登记白名单),可达即密封破口
            foreach (HadalBasin b in Plan.Basins) {
                if (Reached(b.CX, b.CY, 4)) {
                    rep.BasinBreached++;
                }
                rep.BasinVolume += (long)(MathF.PI * b.RX * b.RY);
            }
            return rep;
        }

        /// <summary>材质直方图(渲染统计/报告)</summary>
        internal long[] Histogram() {
            var histogram = new long[16];
            foreach (byte b in Mat) {
                histogram[b]++;
            }
            return histogram;
        }
    }

    //洪泛报告:harness打印+游戏侧GenReport引用同名指标
    internal sealed class HadalFloodReport
    {
        internal bool SpawnStandOk;
        internal long EnvelopeTotal;
        internal long Visited;
        internal List<(string name, bool ok)> Waypoints = [];
        internal int BasinBreached;
        internal long BasinVolume;

        internal double Coverage => EnvelopeTotal > 0 ? Visited * 100.0 / EnvelopeTotal : 0.0;
    }
}
