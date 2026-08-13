using Terraria;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Passes
{
    //P30:层规划(纯数据零tile写入,§1.5"数据规划与tile写入切开")
    //Wave-1只对L1/L2建上下文,其余层带保持M0空脊形态;
    //宏观既成结构(脊/竖井/安全房)与禁室足印先行登记进占用栅格,
    //使P50层内容落房与P45禁室盖章构造性互不切坏(§3.2-3)
    internal class LayerPlanPass : GenPass
    {
        public LayerPlanPass() : base("Dungeonworld Layer Plan", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "规划层内房间图...";
            LayerPlans.Reset();
            ScatterEngine.ResetCounters();

            LayerPlans.L1 = BuildContext(DungeonworldMetrics.Bands[0]);
            LayerPlans.L2 = BuildContext(DungeonworldMetrics.Bands[1]);
            progress.Set(0.5);

            //L1:教堂占位安全房足印(P20已刻画,登记为既成事实)
            LayerBuildContext l1 = LayerPlans.L1;
            l1.Grid.MarkUnchecked(Inflate(new Rectangle(
                DungeonworldMetrics.SafeRoomLeft,
                l1.Band.SpineFloorTop - DungeonworldMetrics.SafeRoomHeight,
                DungeonworldMetrics.SafeRoomWidth, DungeonworldMetrics.SafeRoomHeight),
                DungeonworldMetrics.RoomPadding));

            //L2:深牢禁室足印时序定论(Wave-1)——选址从P45盖章期提前到本规划期定点,
            //足印+padding先行预留,层内容房间构造性避开;P45只按已定坐标盖章。
            //genRand消耗点固定在P30且先于一切层内容随机(R4:随机流顺序与内容解耦)
            Point? bossOrigin = GaolBossRoomSiting.PickOrigin();
            if (bossOrigin is Point origin) {
                Rectangle bounds = BossRooms.GaolBossRoom.Bounds(origin);
                LayerPlans.L2.Grid.MarkUnchecked(Inflate(bounds, DungeonworldMetrics.RoomPadding));
                //Boss房开阔区零撒布(§3.2-7特例)
                LayerPlans.ScatterExclusions.Add(Inflate(bounds, 1));
            }

            CWRMod.Instance.Logger.Info(
                "[Dungeonworld] P30 LayerPlan L1/L2上下文就绪"
                + $" bossOrigin={(bossOrigin.HasValue ? $"({bossOrigin.Value.X},{bossOrigin.Value.Y})" : "none")}");
            progress.Set(1.0);
        }

        //上下文+宏观既成结构登记:脊走廊(顶板缓冲1行起到带底)与主竖井(±2侧壁)
        //房间可用区=脊内膛顶以上的带内空间,与M0几何零冲突
        private static LayerBuildContext BuildContext(LayerBand band) {
            var ctx = new LayerBuildContext(band);
            ctx.Grid.MarkUnchecked(new Rectangle(
                DungeonworldMetrics.BorderThick, band.SpineInteriorTop - 1,
                DungeonworldMetrics.Width - DungeonworldMetrics.BorderThick * 2,
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
