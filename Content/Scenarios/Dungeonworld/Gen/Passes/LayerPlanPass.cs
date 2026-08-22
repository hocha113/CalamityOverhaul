using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Passes
{
    //P30:层规划(纯数据零tile写入,§1.5"数据规划与tile写入切开")
    //Wave-2:七带全部建上下文(L1~L7);宏观既成结构(脊/主竖井/安全房)、
    //隔离带楼梯井足印(井位P20已定,见VerticalLinks)与禁室足印先行登记进
    //占用栅格,使P50层内容落房与P45禁室盖章构造性互不切坏(§3.2-3)
    //
    //===随机消耗顺序纪律(R4,全链路定序,改动必须同步本注释)===
    //P20:隔离带井位x6(先竖直连接)→P30:禁室定点x2(后逐层,选址内部先扣触井
    //禁带)→P50:各层内容自上而下，本pass自身零随机消耗;
    //新增层级定点类随机务必排在禁室之后并在此登记
    internal class LayerPlanPass : GenPass
    {
        public LayerPlanPass() : base("Dungeonworld Layer Plan", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "规划层内房间图...";
            LayerPlans.Reset();
            ScatterEngine.ResetCounters();

            //七带占用栅格内存账(R5护栏):栅格=bool[宽1916,带行数],每格1字节
            //L1/L2(150行)各≈0.29MB,L3弹性带(1348行)≈2.58MB,L4(1000)≈1.92MB,
            //L5(1400)≈2.68MB,L6(1200)≈2.30MB,L7(220)≈0.42MB,合计≈10.5MB,
            //低于P80洪泛visited矩阵(12MB);单次生成生命周期,Reset置null可回收
            LayerBand[] bands = DungeonworldMetrics.Bands;
            LayerPlans.L1 = BuildContext(bands[0]);
            LayerPlans.L2 = BuildContext(bands[1]);
            LayerPlans.L3 = BuildContext(bands[2]);
            LayerPlans.L4 = BuildContext(bands[3]);
            LayerPlans.L5 = BuildContext(bands[4]);
            LayerPlans.L6 = BuildContext(bands[5]);
            LayerPlans.L7 = BuildContext(bands[6]);
            progress.Set(0.4);

            //先竖直连接:楼梯井足印预留进相邻两带ctx.Grid(井位P20已定,零随机)
            VerticalLinks.ReserveInto();
            progress.Set(0.6);

            //L1:教堂占位安全房足印(P20已刻画,登记为既成事实)
            LayerBuildContext l1 = LayerPlans.L1;
            l1.Grid.MarkUnchecked(Inflate(new Rectangle(
                DungeonworldMetrics.SafeRoomLeft,
                l1.Band.SpineFloorTop - DungeonworldMetrics.SafeRoomHeight,
                DungeonworldMetrics.SafeRoomWidth, DungeonworldMetrics.SafeRoomHeight),
                DungeonworldMetrics.RoomPadding));

            //后逐层:L2深牢禁室定点(Wave-1定论沿革)：选址在规划期定点,
            //足印+padding先行预留,层内容房间构造性避开;P45只按已定坐标盖章;
            //Wave-2追加:选址内部扣除触井禁带(避让方向=禁室避井,见GaolBossRoomSiting)
            Point? bossOrigin = GaolBossRoomSiting.PickOrigin();
            if (bossOrigin is Point origin) {
                Rectangle bounds = BossRooms.GaolBossRoom.Bounds(origin);
                LayerPlans.L2.Grid.MarkUnchecked(Inflate(bounds, DungeonworldMetrics.RoomPadding));
                //Boss房开阔区零撒布(§3.2-7特例)
                LayerPlans.ScatterExclusions.Add(Inflate(bounds, 1));
            }

            CWRMod.Instance.Logger.Info(
                "[Dungeonworld] P30 LayerPlan L1~L7上下文就绪"
                + $" wells=[{VerticalLinks.Summary()}]"
                + $" bossOrigin={(bossOrigin.HasValue ? $"({bossOrigin.Value.X},{bossOrigin.Value.Y})" : "none")}");
            progress.Set(1.0);
        }

        //上下文+宏观既成结构登记:脊走廊(顶板缓冲1行起到带底)与主竖井(±2侧壁)
        //房间可用区=脊内膛顶以上的带内空间,与M0几何零冲突
        private static LayerBuildContext BuildContext(LayerBand band) {
            var ctx = new LayerBuildContext(band);
            ctx.Grid.MarkUnchecked(new Rectangle(
                DungeonworldMetrics.PlayLeft, band.SpineInteriorTop - 1,
                DungeonworldMetrics.PlayRight - DungeonworldMetrics.PlayLeft,
                band.Bottom - (band.SpineInteriorTop - 1)));
            ctx.Grid.MarkUnchecked(new Rectangle(
                DungeonworldMetrics.ShaftLeft - 2, band.Top,
                DungeonworldMetrics.ShaftWidth + 4, band.Bottom - band.Top));
            return ctx;
        }

        private static Rectangle Inflate(Rectangle r, int pad)
            => new(r.X - pad, r.Y - pad, r.Width + pad * 2, r.Height + pad * 2);
    }
}
