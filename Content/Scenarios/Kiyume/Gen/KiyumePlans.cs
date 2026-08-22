using Terraria;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Gen
{
    //跨 pass 的规划态：骨架 pass 写，后续 pass 只读
    //每次进世界重生成，生成 pass 先于 OnWorldLoad 运行，复位必须在骨架 pass 里做
    internal static class KiyumePlans
    {
        /// <summary>逐列地板顶行（第一格实心的行号）；骨架 pass 产出</summary>
        internal static int[] FloorTop;

        internal static void Reset() {
            FloorTop = null;
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
            return (int)KiyumeMetrics.BaseFloorAt(x);
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
    }
}
