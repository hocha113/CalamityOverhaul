using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen
{
    //层建造上下文:P30 LayerPlanPass产出,P50层内容入口与P55撒布消费(brief §2.5接缝契约)
    //
    //===层内容入口契约(Wave-1,L1/L2路照此交付,父级缝合接线)===
    //文件: Gen\Layers\L{n}\L{n}Content.cs
    //签名: internal static class L{n}Content { internal static void PlanAndBuild(LayerBuildContext ctx); }
    //纪律:
    //1.随机只用WorldGen.genRand(F22决定论,禁Main.rand);
    //2.房间落位必须先过ctx.Grid预留(RoomPlacer.TryPlace已内建),失败=缩房/放弃禁止硬写;
    //  脊走廊/主竖井/安全房/禁室足印已由P30先行MarkUnchecked,构造性互斥;
    //3.tile写入只走TileBrush/CorridorRouter/Prefab,家具统一WorldGen.PlaceObject(F9);
    //4.禁止跨越已预留宏观足印的门对门长走廊(禁室内联在脊上,脊本身即穿越路径);
    //5.撒布类装修不在入口内直放,声明进ctx.Scatter由P55统一执行(密度档见D表ROOMS-INDEX §7);
    //6.几何随入口返回冻结,放不下fail loud不静默修补(§3.1)。
    internal sealed class LayerBuildContext(LayerBand band)
    {
        internal readonly LayerBand Band = band;

        //管辖=该带内膛(扣除左右钳制死区,见PlayLeft/PlayRight),越带预留天然被拒(§1.2隔离带纪律)
        internal readonly OccupancyGrid Grid = new(new Rectangle(
            DungeonworldMetrics.PlayLeft, band.Top,
            DungeonworldMetrics.PlayRight - DungeonworldMetrics.PlayLeft,
            band.Bottom - band.Top));

        internal readonly RoomGraph Graph = new();

        //层声明的撒布装修数据,P55 ScatterPass消费
        internal readonly List<ScatterEntry> Scatter = [];
    }

    //每次生成重算的规划态(ShouldSave=false回放制),P30入口重置
    //Wave-2:七带全槽——L3~L7上下文由P30一并建立(含隔离带楼梯井足印预留,
    //见VerticalLinks),层内容入口按波次接线(P50 LayerContentPass调度)
    internal static class LayerPlans
    {
        internal static LayerBuildContext L1;
        internal static LayerBuildContext L2;
        internal static LayerBuildContext L3;
        internal static LayerBuildContext L4;
        internal static LayerBuildContext L5;
        internal static LayerBuildContext L6;
        internal static LayerBuildContext L7;

        //撒布禁区:Boss房开阔区零撒布(§3.2-7特例)
        internal static readonly List<Rectangle> ScatterExclusions = [];

        /// <summary>按层带索引取上下文(与DungeonworldMetrics.Bands同索引),越界返回null</summary>
        internal static LayerBuildContext ByIndex(int bandIndex) => bandIndex switch {
            0 => L1, 1 => L2, 2 => L3, 3 => L4, 4 => L5, 5 => L6, 6 => L7,
            _ => null,
        };

        internal static void Reset() {
            L1 = L2 = L3 = L4 = L5 = L6 = L7 = null;
            ScatterExclusions.Clear();
        }
    }
}
