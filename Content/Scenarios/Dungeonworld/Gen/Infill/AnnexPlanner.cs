using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Infill
{
    //====================================================================
    //封存副翼规划器(P54消费)。
    //
    //===要解决的问题===
    //L1只用中央约860列、L2用SpawnX±600、L6用SpawnX±780,1916列的可用宽度里
    //两侧各留着几百列全实心。原设计把这当"隔离哲学"的留白,但玩家的读法是
    //"走到边上就撞墙"。本器把留白改成低密度的封存建筑群:地牢还在延伸,
    //只是被封存了——密度只给主区的三分之一,留白感保住,墙感去掉。
    //
    //===为什么能安全落地===
    //P20的层脊走廊本来就横贯整个可用宽度(PlayLeft~PlayRight),留白区的脊
    //早就通到边上了。所以副翼不需要新开任何跨区通道:沿用主内容同一套
    //"梳齿挂房"语法把房挂在脊上方即可,连通性与主区同源。
    //
    //门面纪律:封存门面只做材质与框缘(过梁换裂砖、顶角斜切、做旧漆、烛台),
    //不在脊里加任何几何——脊是全层的穿越动脉,窄一格都不许。
    //裂砖只用在L6(INDEX §3认领表:裂砖假地板禁用L1/L3/L4/L7),
    //L1/L2的过梁改用本层Slab墙+做旧漆表达。
    //====================================================================
    internal static class AnnexPlanner
    {
        private const int MinWingWidth = 120;
        //副翼层高档:比夹层略高,房间更疏,读法才是"另一片区"而不是"夹缝"
        private const int TierRowsMin = 58;
        private const int TierRowsMax = 76;
        private const int MaxTiers = 3;
        private const int CorridorBandTop = 6;
        private const int CorridorBandRows = 9;
        private const int RoomHang = 10;
        private const int CorridorSpanMin = 40;
        private const int GrowStep = 6;
        //脊上方第一级:留5行才躲得开P30从SpineInteriorTop-1起的脊预留(含RoomPadding外扩)
        private const int SpineAnchorLift = 5;
        //房间横向最小间隔:副翼的稀疏感靠它,不靠少放房
        private const int RoomGapMin = 26;
        private const int PortalWidth = 7;

        internal struct Report
        {
            internal int Wings;
            internal int Tiers;
            internal int Rooms;
            internal int Shafts;
            internal int Portals;
            internal int Rewards;
            internal int FurnPlaced;
            internal int FurnRejected;

            public override readonly string ToString()
                => $"翼{Wings} 层{Tiers} 房{Rooms} 井{Shafts} 门面{Portals}"
                + $" 大奖{Rewards} 家具{FurnPlaced}成/{FurnRejected}拒";
        }

        /// <summary>层带主入口:定活跃区边界→左右两翼各建一组</summary>
        internal static Report Build(LayerBuildContext ctx, InfillSkin skin, UnifiedRandom rand) {
            var report = new Report();
            LayerBand band = ctx.Band;
            int activeLeft = FindActiveEdge(ctx.Grid, band, fromLeft: true);
            int activeRight = FindActiveEdge(ctx.Grid, band, fromLeft: false);
            if (activeLeft < 0 || activeRight < 0) {
                CWRMod.Instance.Logger.Warn(
                    $"[Annex] {band.Name}未探到活跃区边界(整带空?),跳过");
                return report;
            }

            int leftEnd = activeLeft - 8;
            if (leftEnd - (DungeonworldMetrics.PlayLeft + 6) >= MinWingWidth) {
                BuildWing(ctx, skin, rand, DungeonworldMetrics.PlayLeft + 6, leftEnd,
                    portalAtRight: true, ref report);
            }
            int rightStart = activeRight + 8;
            if (DungeonworldMetrics.PlayRight - 6 - rightStart >= MinWingWidth) {
                BuildWing(ctx, skin, rand, rightStart, DungeonworldMetrics.PlayRight - 6,
                    portalAtRight: false, ref report);
            }
            return report;
        }

        //===活跃区边界:自钳制线向内扫,第一根"脊以上有内容"的列即边界===
        //从边缘向内早退,扫过的只有留白区那几百列,不是全带扫描(R5)
        private static int FindActiveEdge(OccupancyGrid grid, LayerBand band, bool fromLeft) {
            int top = band.Top + 2;
            int height = band.SpineInteriorTop - 2 - top;
            if (height <= 0) {
                return -1;
            }
            int span = DungeonworldMetrics.PlayRight - DungeonworldMetrics.PlayLeft;
            for (int step = 0; step < span; step++) {
                int x = fromLeft ? DungeonworldMetrics.PlayLeft + step
                    : DungeonworldMetrics.PlayRight - 1 - step;
                if (!grid.CanReserve(new Rectangle(x, top, 1, height), 0)) {
                    return x;
                }
            }
            return -1;
        }

        //===单翼:自脊向上堆层,每层一条封存廊+若干封存房===
        private static void BuildWing(LayerBuildContext ctx, InfillSkin skin, UnifiedRandom rand,
            int wingLeft, int wingRight, bool portalAtRight, ref Report report) {
            LayerBand band = ctx.Band;
            int ceilingLimit = band.Top + 8;
            int spineAnchor = band.SpineInteriorTop - SpineAnchorLift;

            //各级地板行先一次算完(纯数据),再自下而上刻画。
            //必须先算:挂房是往廊上方长的,不先知道上一级在哪,下一级的房就会
            //把上一级廊的位置吃掉,上面那级只能整级落空
            var floors = new List<int>(MaxTiers);
            int floorCursor = spineAnchor;
            while (floors.Count < MaxTiers
                && floorCursor - CorridorBandTop - RoomHang - InfillRooms.RoomMinClearance >= ceilingLimit) {
                floors.Add(floorCursor);
                floorCursor -= rand.Next(TierRowsMin, TierRowsMax + 1);
            }
            if (floors.Count == 0) {
                return;
            }

            int prevFloor = -1;
            int prevIndex = -1;
            bool anyTier = false;
            for (int t = 0; t < floors.Count; t++) {
                int corridorFloor = floors[t];
                if (!GrowSpan(ctx.Grid, corridorFloor, wingLeft, wingRight,
                    out int spanL, out int spanR)) {
                    break;
                }
                ctx.Grid.MarkUnchecked(new Rectangle(spanL, corridorFloor - CorridorBandTop,
                    spanR - spanL, CorridorBandRows));

                InfillRooms.Tally tally = InfillRooms.ServiceCorridor(
                    spanL, spanR, corridorFloor, skin, rand);
                report.FurnPlaced += tally.Placed;
                report.FurnRejected += tally.Rejected;

                var corridor = new RoomNode {
                    Bounds = new Rectangle(spanL, corridorFloor - InfillRooms.CorridorClearance
                        - DungeonworldMetrics.RoomShellThick, spanR - spanL,
                        InfillRooms.CorridorClearance + DungeonworldMetrics.RoomShellThick * 2),
                };
                int corridorIndex = ctx.Graph.Rooms.Count;
                ctx.Graph.Rooms.Add(corridor);
                report.Tiers++;
                anyTier = true;

                //下接:第0级落层脊(副翼的唯一入口),其余级落下一级封存廊。
                //井柱一并落账,免得后来的房把壳当成实心岩(实际是这根井)
                if (t == 0) {
                    int downX = portalAtRight ? spanR - 8 : spanL + 5;
                    DropShaft(ctx, skin, downX, corridorFloor, band.SpineFloorTop, ref report);
                    //长廊再补一口,免得整条副翼只有一个出入口
                    if (spanR - spanL > 200) {
                        DropShaft(ctx, skin, portalAtRight ? spanL + 5 : spanR - 8,
                            corridorFloor, band.SpineFloorTop, ref report);
                    }
                }
                else {
                    int downX = System.Math.Clamp((spanL + spanR) / 2, spanL + 4,
                        spanR - DungeonworldMetrics.StairWellWidth - 4);
                    DropShaft(ctx, skin, downX, corridorFloor, prevFloor, ref report);
                    ctx.Graph.Edges.Add(new RoomEdge(prevIndex, corridorIndex,
                        SocketKind.PlatformGap, EdgeForm.StairWell));
                }

                int hungFloor = corridorFloor - RoomHang;
                //挂房净高上限=上一级廊预留带的底,顶到带顶为止
                int roomCeiling = t + 1 < floors.Count
                    ? floors[t + 1] + CorridorBandRows - CorridorBandTop
                    : ceilingLimit;
                int maxH = hungFloor - roomCeiling - DungeonworldMetrics.RoomShellThick;
                HangSealedRooms(ctx, skin, rand, corridorIndex, spanL, spanR, hungFloor, maxH,
                    corridorFloor, t == 0, ref report);

                prevFloor = corridorFloor;
                prevIndex = corridorIndex;
            }

            if (!anyTier) {
                return;
            }
            report.Wings++;
            //门面开在活跃区一侧的脊上,让玩家从主区看得见"那边还有东西"
            int portalX = portalAtRight ? wingRight - PortalWidth - 2 : wingLeft + 2;
            if (StampPortal(portalX, band, skin, ref report)) {
                report.Portals++;
            }
        }

        //井柱刻画+落账:井穿的是两级廊之间的岩,那段行不在任何一级的预留带里,
        //不登记的话后来的房会把这根已凿空的柱当实心壳用
        private static void DropShaft(LayerBuildContext ctx, InfillSkin skin, int x,
            int floorTopUpper, int floorTopLower, ref Report report) {
            ctx.Grid.MarkUnchecked(new Rectangle(x - 1, floorTopUpper,
                DungeonworldMetrics.StairWellWidth + 2, floorTopLower - floorTopUpper));
            InfillRooms.BareShaft(x, floorTopUpper, floorTopLower, skin);
            report.Shafts++;
        }

        //===封存廊横向生长:填满整翼,撞上足印即停===
        private static bool GrowSpan(OccupancyGrid grid, int corridorFloor,
            int wingLeft, int wingRight, out int spanL, out int spanR) {
            int top = corridorFloor - CorridorBandTop;
            spanL = wingLeft;
            spanR = wingLeft + CorridorSpanMin;
            if (spanR > wingRight
                || !grid.CanReserve(new Rectangle(spanL, top, spanR - spanL, CorridorBandRows), 0)) {
                //起点被占就沿翼向内挪,整翼扫不出起点才放弃
                bool found = false;
                for (int x = wingLeft; x + CorridorSpanMin <= wingRight; x += GrowStep) {
                    if (grid.CanReserve(new Rectangle(x, top, CorridorSpanMin, CorridorBandRows), 0)) {
                        spanL = x;
                        spanR = x + CorridorSpanMin;
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    return false;
                }
            }
            while (spanR + GrowStep <= wingRight
                && grid.CanReserve(new Rectangle(spanR, top, GrowStep, CorridorBandRows), 0)) {
                spanR += GrowStep;
            }
            return spanR - spanL >= CorridorSpanMin;
        }

        //===封存房:沿廊按最小间隔铺开,密度只给主区三分之一===
        private static void HangSealedRooms(LayerBuildContext ctx, InfillSkin skin, UnifiedRandom rand,
            int corridorIndex, int spanL, int spanR, int hungFloor, int maxH, int corridorFloor,
            bool rewardTier, ref Report report) {
            if (maxH < InfillRooms.RoomMinClearance) {
                return;
            }
            int cursor = spanL + 4;
            int placedHere = 0;
            RoomNode outermost = null;
            while (cursor < spanR - 12) {
                bool rubble = maxH >= 8 && rand.NextBool(2);
                Point size = rubble ? InfillRooms.RubbleInteriorSize(rand)
                    : InfillRooms.UtilityInteriorSize(rand);
                size.Y = System.Math.Min(size.Y, maxH);
                if (size.Y < InfillRooms.RoomMinClearance) {
                    break;
                }
                int slotRight = System.Math.Min(spanR - 4, cursor + size.X + 10);
                RoomNode room = RoomPlacer.TryPlace(ctx.Grid, rand, cursor, slotRight,
                    hungFloor, size, size, retries: 6);
                if (room == null) {
                    cursor += RoomGapMin;
                    continue;
                }

                InfillRooms.Tally tally = rubble
                    ? InfillRooms.BuildRubble(room, skin, rand)
                    : InfillRooms.BuildUtility(room, skin, rand);
                report.FurnPlaced += tally.Placed;
                report.FurnRejected += tally.Rejected;

                int roomIndex = ctx.Graph.Rooms.Count;
                ctx.Graph.Rooms.Add(room);
                InfillRooms.MaintenanceShaft(room, DungeonworldMetrics.RoomShellThick,
                    corridorFloor, skin);
                ctx.Graph.Edges.Add(new RoomEdge(roomIndex, corridorIndex,
                    SocketKind.PlatformGap, EdgeForm.StairWell));
                report.Shafts++;
                report.Rooms++;
                placedHere++;
                outermost ??= room;
                //稀疏感:房与房之间必须留一段没动过的岩
                cursor = room.Bounds.Right + RoomGapMin + rand.Next(0, 24);
            }

            //每翼一个够分量的箱,放在最外那间——让这趟路值得走
            if (rewardTier && outermost != null) {
                int cx = outermost.InteriorRight - 3;
                int cy = outermost.FloorTop - 1;
                bool ok = WorldGen.PlaceChest(cx, cy, TileID.Containers,
                    notNearOtherChests: false, skin.ChestRewardStyle) >= 0;
                if (ok) {
                    outermost.Role = RoomRole.Treasure;
                    report.Rewards++;
                }
                report.FurnPlaced += ok ? 1 : 0;
                report.FurnRejected += ok ? 0 : 1;
            }
            if (placedHere == 0) {
                CWRMod.Instance.Logger.Info(
                    $"[Annex] 廊[{spanL},{spanR})零封存房(净高{maxH}),该级只成廊");
            }
        }

        //===封存门面:只改材质与框缘,脊里一格几何都不加===
        private static bool StampPortal(int x, LayerBand band, InfillSkin skin, ref Report report) {
            int lintel = band.SpineInteriorTop - 1;
            int floor = band.SpineFloorTop;
            if (x < DungeonworldMetrics.PlayLeft + 2
                || x + PortalWidth > DungeonworldMetrics.PlayRight - 2) {
                return false;
            }
            //过梁材质按认领表取(见 InfillSkin.AllowCrackedLintel)
            ushort lintelBrick = skin.AllowCrackedFloor ? skin.CrackedBrick : skin.Brick;
            for (int i = 0; i < PortalWidth; i++) {
                TileBrush.SetSolid(x + i, lintel, lintelBrick);
            }
            //顶两角斜切成拱:切的是天花实心格,净空不减反增(§2.5-3拱角收角)
            TileBrush.SetSloped(x, lintel, skin.Brick, SlopeType.SlopeUpRight);
            TileBrush.SetSloped(x + PortalWidth - 1, lintel, skin.Brick, SlopeType.SlopeUpLeft);
            //门槛 + 做旧漆:封存感全靠这两层,不动碰撞几何(§3.2-6)
            for (int i = 1; i < PortalWidth - 1; i++) {
                TileBrush.SetSolid(x + i, floor, lintelBrick);
            }
            LayerTint.Wash(new Rectangle(x, band.SpineInteriorTop, PortalWidth, floor - band.SpineInteriorTop),
                skin.AgePaint, 85, skin.PatchSalt ^ 0x1D, skin.WallFamily, skin.BrickFamily);
            //两侧烛台标出门口
            InfillRooms.TryPlaceTile(x + 1, floor - 1, TileID.Candelabras, skin.CandelabraStyle);
            InfillRooms.TryPlaceTile(x + PortalWidth - 2, floor - 1, TileID.Candelabras,
                skin.CandelabraStyle);
            return true;
        }
    }
}
