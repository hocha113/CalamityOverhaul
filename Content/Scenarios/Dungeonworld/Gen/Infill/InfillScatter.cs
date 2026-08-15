using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Infill
{
    //====================================================================
    //填充区的撒布补档。
    //
    //===为什么只补一点点===
    //ScatterEngine是按层带面积撒点、逐点做局部合法性验证的(F30三段模式),
    //所以各层在P50声明的那些条目本来就会落进P52/P54新凿的房——填充区不是
    //"没人管的空白",它一开始就吃着本层的撒布。真正的问题只有一个:目标点数
    //是按带面积算的定值,凿空量变大之后同样的点数摊得更薄。
    //
    //===所以补什么===
    //只补两样,都严格避开各层已声明的条目,不搞双重覆盖(P55头注释明令):
    //① 碎石堆——各层撒布表里都没有的新词,而且正是后勤面/塌方该有的东西;
    //② 地牢罐——只给撒布表里确实没有罐的三带(L1/L5/L6);
    //   L2的罐在P55内置条目里、L4的罐在L4Scatter里,那两带一件不加。
    //蛛网/骨堆/挂画/旗帜一概不碰:INDEX §7矩阵已把它们判给各层自己。
    //====================================================================
    internal static class InfillScatter
    {
        //碎石堆:tile185第1行(2x1)样式0~5=石砾堆,6~11才是骨(L2Palette已对源核实);
        //PlaceSmallPile自带SolidTile2锚定与占位校验,拒绝即计失败
        private static ScatterEntry DebrisPiles(ScatterDensity density) => new() {
            Name = "碎石堆", Density = density, StandardPer100k = 10, DedupeDist = 9, MaxPlaced = 70,
            TryPlace = static (x, y) => OnFloor(x, y)
                && WorldGen.PlaceSmallPile(x, y, WorldGen.genRand.Next(0, 6), 1),
        };

        /// <summary>按层带索引取补档条目;附加进 ctx.Scatter 由P55统一执行</summary>
        internal static IEnumerable<ScatterEntry> For(int bandIndex) {
            yield return DebrisPiles(bandIndex == 4 ? ScatterDensity.High : ScatterDensity.Standard);
            //罐:只给自家撒布表里没有罐的三带
            if (bandIndex is 0 or 4 or 5) {
                yield return CommonScatter.DungeonPots(ScatterDensity.Low);
            }
        }

        //轻量预检:落点空+脚下实心非平台(镜像CommonScatter.OnFloor)
        private static bool OnFloor(int x, int y) {
            if (Main.tile[x, y].HasTile) {
                return false;
            }
            Tile below = Main.tile[x, y + 1];
            return below.HasTile && Main.tileSolid[below.TileType]
                && below.TileType != TileID.Platforms;
        }
    }
}
