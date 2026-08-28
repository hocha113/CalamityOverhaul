using System.Collections.Generic;
using Terraria;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Gen
{
    /// <summary>洼地登记：半开列区间与水面行，骨架 pass 产出，灌水/避让/水面渲染共读</summary>
    internal readonly struct KiamePoolSpan
    {
        internal readonly int Left;
        internal readonly int Right;      //含
        internal readonly int SurfaceRow; //水面顶行（该行即水）

        internal KiamePoolSpan(int left, int right, int surfaceRow) {
            Left = left;
            Right = right;
            SurfaceRow = surfaceRow;
        }

        internal bool Overlaps(int left, int right) => left <= Right && right >= Left;
    }

    //跨 pass 的规划态：骨架 pass 写，后续 pass 只读
    //每次进世界重生成，生成 pass 先于 OnWorldLoad 运行，复位必须在骨架 pass 里做
    internal static class KiamePlans
    {
        /// <summary>逐列地板顶行（第一格实心的行号）；骨架 pass 产出</summary>
        internal static int[] FloorTop;

        /// <summary>洼地登记表；骨架 pass 产出，Terrain 灌水、结构避让、撒布让路共读</summary>
        internal static readonly List<KiamePoolSpan> Pools = [];

        /// <summary>结构足印禁区（半开列区间）：村屋/井等登记，撒布让路</summary>
        private static readonly List<(int Left, int Right)> exclusions = [];

        internal static void Reset() {
            FloorTop = null;
            Pools.Clear();
            exclusions.Clear();
        }

        //SubLib 不会把 progress.Message 抄到 Main.statusText。
        //加载屏若只读 statusText，就会一直停在 clearWorld 的「正在清除地图数据」。
        internal static void Report(GenerationProgress progress, string message) {
            progress.Message = message;
            Main.statusText = message;
        }

        /// <summary>安全取列地板顶行；规划未就绪时回退基准曲线</summary>
        internal static int FloorTopAt(int x) {
            int[] top = FloorTop;
            if (top != null && x >= 0 && x < top.Length) {
                return top[x];
            }
            return (int)KiameMetrics.BaseFloorAt(x);
        }

        /// <summary>该列地板顶的世界 Y（px）</summary>
        internal static float FloorWorldY(int x) => FloorTopAt(x) * 16f;

        /// <summary>从起始行向下探该列第一格实心；找不到回退规划值</summary>
        internal static int ProbeGround(int x, int fromRow) {
            for (int y = fromRow; y < Main.maxTilesY - 1; y++) {
                Tile tile = Framing.GetTileSafely(x, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    return y;
                }
            }
            return FloorTopAt(x);
        }

        /// <summary>列区间是否压到任何洼地（含外扩余量）</summary>
        internal static bool OverlapsPool(int left, int right, int margin = 0) {
            foreach (KiamePoolSpan pool in Pools) {
                if (pool.Overlaps(left - margin, right + margin)) {
                    return true;
                }
            }
            return false;
        }

        internal static void RegisterExclusion(int left, int right) => exclusions.Add((left, right));

        /// <summary>该列是否落在结构禁区里</summary>
        internal static bool InExclusion(int x) {
            foreach ((int left, int right) in exclusions) {
                if (x >= left && x < right) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>列区间是否压到任何结构禁区</summary>
        internal static bool OverlapsExclusion(int left, int right) {
            foreach ((int exLeft, int exRight) in exclusions) {
                if (left < exRight && right >= exLeft) {
                    return true;
                }
            }
            return false;
        }
    }
}
