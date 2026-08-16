using CalamityOverhaul.Content.Scenarios.OldNet.Gen.Rooms;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen
{
    //带建造上下文：P30 ZonePlanPass产出，P50带内容入口与P55撒布消费
    //
    //===带内容入口契约（镜像 Dungeonworld LayerBuildContext 契约改横向）===
    //文件: Gen\Zones\Z{n}\Z{n}Content.cs
    //签名: internal static class Z{n}Content { internal static void PlanAndBuild(OldNetBuildContext ctx); }
    //纪律:
    //1.随机只用WorldGen.genRand（决定论，禁Main.rand）;
    //2.结构落位必须先过ctx.Grid预留（OldNetRoomPlacer.TryPlace已内建），失败=缩房/放弃禁止硬写;
    //  竖井/平台厅/锚位足印已由P20刻画、P30先行MarkUnchecked，构造性互斥;
    //3.tile写入只走OldNetTileBrush/OldNetPrefab，家具统一WorldGen.PlaceObject;
    //4.撒布类装修不在入口内直放，声明进ctx.Scatter由P55统一执行;
    //5.节点放置一律走OldNetPlans.Budget（配额审计的唯一入口）;
    //6.几何随入口返回冻结，放不下fail loud不静默修补。
    internal sealed class OldNetBuildContext(string name, Rectangle area)
    {
        internal readonly string Name = name;

        /// <summary>管辖矩形（列带全高或高空带横带）</summary>
        internal readonly Rectangle Area = area;

        internal readonly OldNetOccupancyGrid Grid = new(area);

        internal readonly OldNetRoomGraph Graph = new();

        //带声明的撒布装修数据，P55 ScatterPass消费
        internal readonly List<OldNetScatterEntry> Scatter = [];
    }

    //撒布条目：TryPlace=局部合法性验证+放置（成功返回true）
    //SurfaceAnchored=true 时引擎按随机列探地表槽位，y传入即槽位行
    internal sealed class OldNetScatterEntry
    {
        internal string Name;
        /// <summary>目标放置数（绝对量，横向世界不换算面积密度）</summary>
        internal int Target;
        /// <summary>同类去重距离（棋盘距）</summary>
        internal int DedupeDist;
        internal bool SurfaceAnchored;
        internal Func<int, int, bool> TryPlace;
    }

    //节点配额账本：全部节点放置的唯一入口，P80审计配额
    internal sealed class OldNetNodeBudget
    {
        internal int PlainPlaced;
        internal int EncryptPlaced;
        internal int EventPlaced;
        internal int UnderPlainPlaced;

        /// <summary>直写节点tile（自定义节点无TileObjectData，不走PlaceObject）</summary>
        internal static bool WriteNodeTile(int x, int y, int tileType) {
            if (!WorldGen.InWorld(x, y) || Main.tile[x, y].HasTile) {
                return false;
            }
            Tile slot = Main.tile[x, y];
            slot.HasTile = true;
            slot.TileType = (ushort)tileType;
            slot.TileFrameX = 0;
            slot.TileFrameY = 0;
            return true;
        }

        /// <summary>地下房间机会性放普通节点（配额内），供带内容入口调用</summary>
        internal bool TryPlaceUnderPlain(int x, int y) {
            if (UnderPlainPlaced >= OldNetMetrics.NodeUnderPlainCount) {
                return false;
            }
            if (!WriteNodeTile(x, y, ModContent.TileType<Tiles.OldNetDataNodeTile>())) {
                return false;
            }
            UnderPlainPlaced++;
            return true;
        }
    }

    //每次生成重算的规划态（ShouldSave=false回放制），P10入口重置
    //gen期专用：运行时消费方一律读tile，不读这里（MP客户端无此数据）
    internal static class OldNetPlans
    {
        internal static OldNetBuildContext Z1;   //墙脚带
        internal static OldNetBuildContext Z2;   //废墟带
        internal static OldNetBuildContext Z3;   //信号衰减区
        internal static OldNetBuildContext Z4;   //高空带（横带，M3 巨构）

        /// <summary>逐列地板顶行（P10产出，全流水线共用）</summary>
        internal static int[] FloorTop;

        /// <summary>P20宏观足印（竖井/平台厅），P30 MarkUnchecked</summary>
        internal static readonly List<Rectangle> MacroFootprints = [];

        /// <summary>竖井记录：井口列与落点层（引导/校验消费）</summary>
        internal static readonly List<OldNetShaft> Shafts = [];

        /// <summary>P30裁决的中继锚位（surface落点），Z2建造</summary>
        internal static readonly List<Point> RelaySpots = [];

        /// <summary>P30裁决的封锁区盒（surface基准），Z2建造</summary>
        internal static readonly List<Rectangle> SealBoxes = [];

        /// <summary>撒布禁区（锚位周边零撒布）</summary>
        internal static readonly List<Rectangle> ScatterExclusions = [];

        internal static OldNetNodeBudget Budget = new();

        internal static void Reset() {
            Z1 = Z2 = Z3 = Z4 = null;
            FloorTop = null;
            MacroFootprints.Clear();
            Shafts.Clear();
            RelaySpots.Clear();
            SealBoxes.Clear();
            ScatterExclusions.Clear();
            Budget = new OldNetNodeBudget();
        }

        internal static bool InScatterExclusion(int x, int y) {
            foreach (Rectangle rect in ScatterExclusions) {
                if (rect.Contains(x, y)) {
                    return true;
                }
            }
            return false;
        }
    }

    //竖井记录：col=井左缘列，surfaceRow=井口行，landing=落点平台厅矩形
    internal readonly struct OldNetShaft(int col, int surfaceRow, Rectangle landing, bool deep)
    {
        internal readonly int Col = col;
        internal readonly int SurfaceRow = surfaceRow;
        internal readonly Rectangle Landing = landing;
        /// <summary>true=深层井（自浅层平台厅继续向下）</summary>
        internal readonly bool Deep = deep;
    }
}
