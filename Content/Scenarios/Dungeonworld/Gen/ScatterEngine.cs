using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen
{
    //撒布密度档位,引用D表ROOMS-INDEX §7密度矩阵(零/低/标/高/峰)
    internal enum ScatterDensity { Zero, Low, Standard, High, Peak }

    //撒布条目:层装修数据的声明单位;TryPlace=局部合法性验证+放置(成功返回true)
    internal sealed class ScatterEntry
    {
        internal string Name;
        internal ScatterDensity Density;
        //"标"档每10万格带面积目标落点数(原版密度按maxTilesX线性,本世界换轴层带面积,F30/§3.1-5)
        internal double StandardPer100k;
        //同类去重距离(棋盘距,F30三段模式的去重项)
        internal int DedupeDist;
        //单条目硬放置上限:生成耗时预算保险(R5,进世界预算<3min)
        internal int MaxPlaced;
        internal Func<int, int, bool> TryPlace;
    }

    //通用撒布引擎:随机撒点→局部合法性验证→失败计数器保底退出(原版三段模式F30)
    //随机全走WorldGen.genRand(F22);禁区由LayerPlans.ScatterExclusions供给
    internal static class ScatterEngine
    {
        internal static long TotalPlaced;
        internal static long TotalAttempts;

        internal static void ResetCounters() => TotalPlaced = TotalAttempts = 0;

        private static double Multiplier(ScatterDensity d) => d switch {
            ScatterDensity.Zero => 0.0,
            ScatterDensity.Low => 0.5,
            ScatterDensity.High => 2.0,
            ScatterDensity.Peak => 4.0,
            _ => 1.0,
        };

        /// <summary>在层带内膛执行一个撒布条目,返回(放置数,尝试数)。</summary>
        internal static (int placed, int attempts) Run(LayerBand band, ScatterEntry entry) {
            int left = DungeonworldMetrics.PlayLeft + 2;
            int right = DungeonworldMetrics.PlayRight - 2;
            int top = band.Top + 2;
            int bottom = band.Bottom - 2;
            long area = (long)(right - left) * (bottom - top);
            int target = (int)Math.Min(entry.MaxPlaced,
                Math.Round(area / 100_000.0 * entry.StandardPer100k * Multiplier(entry.Density)));
            if (target <= 0) {
                return (0, 0);
            }

            //保底退出:尝试预算=目标x10;层带大半实心时撒点命中率低属预期,不无限重试
            int maxAttempts = target * 10;
            var placedPts = new List<Point>(target);
            int placed = 0, attempts = 0;
            while (placed < target && attempts < maxAttempts) {
                attempts++;
                int x = WorldGen.genRand.Next(left, right);
                int y = WorldGen.genRand.Next(top, bottom);
                if (InExclusion(x, y) || TooClose(placedPts, x, y, entry.DedupeDist)) {
                    continue;
                }
                if (entry.TryPlace(x, y)) {
                    placedPts.Add(new Point(x, y));
                    placed++;
                }
            }
            TotalPlaced += placed;
            TotalAttempts += attempts;
            return (placed, attempts);
        }

        private static bool InExclusion(int x, int y) {
            foreach (Rectangle rect in LayerPlans.ScatterExclusions) {
                if (rect.Contains(x, y)) {
                    return true;
                }
            }
            return false;
        }

        private static bool TooClose(List<Point> pts, int x, int y, int dist) {
            foreach (Point p in pts) {
                if (Math.Abs(p.X - x) < dist && Math.Abs(p.Y - y) < dist) {
                    return true;
                }
            }
            return false;
        }
    }

    //跨层通用撒布条目首批(A路直供;层专属母题由层代理经ctx.Scatter声明)
    //放置一律走原版函数自带锚定/占位校验,拒绝即计失败,绝不强写帧(§3.2-1)
    internal static class CommonScatter
    {
        //蛛网:空格+室内墙+至少一个四邻实心(墙角感);tile 51直放无锚定依赖
        internal static ScatterEntry Cobweb(ScatterDensity density) => new() {
            Name = "蛛网", Density = density, StandardPer100k = 20, DedupeDist = 6, MaxPlaced = 120,
            TryPlace = static (x, y) => {
                Tile t = Main.tile[x, y];
                if (t.HasTile || t.WallType == WallID.None || !AnySolidNeighbor(x, y)) {
                    return false;
                }
                WorldGen.PlaceTile(x, y, TileID.Cobweb, mute: true);
                return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Cobweb;
            },
        };

        //骨堆:2x1骨样式6~15(对源核实WorldGen.cs L14406骨样式专段);
        //PlaceSmallPile自带SolidTile2锚定与占位校验
        internal static ScatterEntry BonePiles(ScatterDensity density) => new() {
            Name = "骨堆", Density = density, StandardPer100k = 14, DedupeDist = 8, MaxPlaced = 80,
            TryPlace = static (x, y) => OnFloor(x, y)
                && WorldGen.PlaceSmallPile(x, y, WorldGen.genRand.Next(6, 16), 1),
        };

        //地牢罐:样式10~12(对源核实WorldGen.cs L13368地牢墙罐样式段);PlacePot自带校验
        internal static ScatterEntry DungeonPots(ScatterDensity density) => new() {
            Name = "罐", Density = density, StandardPer100k = 8, DedupeDist = 10, MaxPlaced = 50,
            TryPlace = static (x, y) => OnFloor(x, y)
                && WorldGen.PlacePot(x, y, 28, WorldGen.genRand.Next(10, 13)),
        };

        //轻量预检:落点空+脚下实心,把明显无效点挡在原版函数之前(省调用)
        private static bool OnFloor(int x, int y) {
            if (Main.tile[x, y].HasTile) {
                return false;
            }
            Tile below = Main.tile[x, y + 1];
            return below.HasTile && Main.tileSolid[below.TileType] && below.TileType != TileID.Platforms;
        }

        private static bool AnySolidNeighbor(int x, int y) {
            return IsSolid(x - 1, y) || IsSolid(x + 1, y) || IsSolid(x, y - 1) || IsSolid(x, y + 1);

            static bool IsSolid(int px, int py) {
                Tile t = Main.tile[px, py];
                return t.HasTile && Main.tileSolid[t.TileType];
            }
        }
    }
}
